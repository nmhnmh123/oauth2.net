using AuthServer.Data;
using AuthServer.HostedServices;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Thêm các dịch vụ (services) cần thiết vào thùng chứa (container)
builder.Services.AddControllersWithViews();

// Cấu hình Database cho AuthServer
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    // Cấu hình Entity Framework Core sử dụng Database lưu trong bộ nhớ (In-Memory).
    // Ở môi trường thực tế, bạn sẽ dùng SQL Server, PostgreSQL, MySQL,...
    options.UseInMemoryDatabase(nameof(ApplicationDbContext));

    // Đăng ký các thực thể (Entity) mặc định mà OpenIddict cần để hoạt động
    // (như bảng để lưu Token, lưu thông tin Ứng dụng client, Scope,...)
    options.UseOpenIddict();
});

// Cấu hình OpenIddict - Trái tim của hệ thống AuthServer
builder.Services.AddOpenIddict()

    // 1. Cấu hình phần Lõi (Core Components)
    .AddCore(options =>
    {
        // Bảo OpenIddict dùng Entity Framework Core và dùng cái Context (Database đính kèm ở trên) để lưu/đọc dữ liệu
        options.UseEntityFrameworkCore()
               .UseDbContext<ApplicationDbContext>();
    })

    // 2. Cấu hình phần Server (Server Components) đóng vai trò là nơi cấp quyền (Authorization Server)
    .AddServer(options =>
    {
        // Kích hoạt (Bật) các cổng giao tiếp (endpoints). Đây là các đường dẫn tiêu chuẩn của chuẩn OAuth2/OIDC.
        options.SetAuthorizationEndpointUris("/connect/authorize") // Điểm bắt đầu quy trình cấp quyền
               .SetEndSessionEndpointUris("/connect/logout")       // Điểm kết thúc phiên đăng nhập (Đăng xuất)
               .SetTokenEndpointUris("/connect/token")             // Điểm đổi Authorization Code lấy Access Token thực sự
               .DisableAccessTokenEncryption();                    // Cực kỳ quan trọng: Phát hành JWT minh bạch (bỏ mã hóa kín) để ResourceApi còn đọc được

        // Thiết lập các quyền (scopes) mặc định mà máy chủ này được phép cấp phát.
        // Ở đây ta có email, profile, roles và một scope custom tự định nghĩa là "api1"
        options.RegisterScopes(
            "openid",
            "email",
            "profile",
            "roles",
            "api1",
            "offline_access");

        // Bật luồng Authorization Code Flow. 
        // Luồng này an toàn nhất cho Web, khi người dùng login xong, hệ thống nhả ra 1 cái "Code",
        // rồi phần dứoi nền (backend) cầm "Code" đó đổi thành "Token" để ngăn hacker ăn cắp Token trên thanh trình duyệt.
        options.AllowAuthorizationCodeFlow()
               .AllowRefreshTokenFlow();

        // Đăng ký chứng chỉ (Certificate) dùng để ký (sign) và mã hóa (encrypt) cái Token.
        // Token cấp ra có chữ ký này thì máy chủ ResourceApi mới tin tưởng là đồ thật.
        // Ở môi trường Dev, OpenIddict tự động tạo cặp key xài tạm. Khi lên Production thực tế phải xài chứng chỉ chuẩn X.509.
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        // Gắn OpenIddict vào cơ chế chung của ASP.NET Core
        options.UseAspNetCore()
               // Bật cơ chế "chuyển hướng" (Pass-through).
               // Có nghĩa là: Thay vì OpenIddict xử lý 100% ngầm, nó cho phép cái Controller (AuthorizationController của chúng ta)
               // Được phép đọc luồng yêu cầu, hiện trang tự làm, sau đó mới gắn kết quả lại vào OpenIddict. (Cho phép tùy biến giao diện Login/Xác nhận)
               .EnableAuthorizationEndpointPassthrough()
               .EnableEndSessionEndpointPassthrough()
               .EnableTokenEndpointPassthrough()
               .EnableStatusCodePagesIntegration()
               .DisableTransportSecurityRequirement(); // QUAN TRỌNG: Cho phép chạy HTTP (không cần HTTPS) ở Local
    })

    // 3. Cấu hình phần Xác thực (Validation Components)
    // AuthServer này đôi lúc cũng cần tự kiểm tra cái Token của chính nó cấp ra (Local validation)
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore(); // Validation cục bộ không gắt gao HTTPS như Server Endpoint
    });

// Cấu hình kỹ thuật Đăng nhập Bằng Cookie truyền thống (Local UI Login).
// Vì OpenIddict chỉ quản lý Giao thức OAuth2, còn việc "Kiểm tra mật khẩu, ghi nhớ người dùng ở trang cấp quyền" 
// thì vẫn là việc của Cookie Auth truyền thống như làm ASP.NET Core MVC bình thường.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Nếu người ta vô trang nào bí mật của cái AuthServer này mà chưa login, tự động đá về "/Account/Login"
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
    });

// Chạy 1 Background task khi ứng dụng vừa bật lên để tự động lưu thông tin về thằng WebClient vào Database.
builder.Services.AddHostedService<TestDataHostedService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // Tuỳ chỉnh bảo mật HSTS cho production
    app.UseHsts();
}

// app.UseHttpsRedirection(); // Tạm tắt để không bị tự đổi sang HTTPS khi đang demo HTTP port 5001
app.UseStaticFiles();

app.UseRouting();

// Bật các rào chắn kiểm tra bảo mật (Đăng nhập và Quyền Hạn)
app.UseAuthentication();
app.UseAuthorization();

// Map route cho các Controller MVC giao diện
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run("http://localhost:5001");
