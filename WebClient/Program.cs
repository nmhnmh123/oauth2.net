using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Khởi tạo các MVC View
builder.Services.AddControllersWithViews();

// CẤU HÌNH CHO ỨNG DỤNG BÊN THỨ 3 (TRANG WEB CỦA KHÁCH) XIN CHỨNG NHẬN (OAUTH2 OIDC CLIENT)
builder.Services.AddAuthentication(options =>
{
    // Xác định bộ quản lý Đăng Nhập Mặc Định là Cookie (Đăng nhập rồi thì ráng nhớ trong Cookie, khỏi login lại)
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    
    // NẾU người dùng nhấp vô trang bí mật mà máy thấy CHƯA CÓ COOKIE -> Lập tức "Thách thức" (Challenge) bằng cách gọi hệ thống OpenIdConnect chạy.
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    // Cài đặt trang bắt đầu
    options.LoginPath = "/login";
})
.AddOpenIdConnect(options =>
{
    // Cài đặt thông số của OPEN ID CONNECT MIDDLEWARE (Sức mạnh vạn năng của Microsoft làm xịn sẵn đỡ code tay)

    // 1. Tòa Thị Chính cấp Token nằm ở đâu? Trả lời: Lão đại tên AuthServer mở cổng 5001.
    options.Authority = "http://localhost:5001/";
    
    // 2. Tên tao là gì khi khai báo với Tòa Thị Chính?
    options.ClientId = "web-client";
    
    // Mật mã riêng để giấu trong két sắt của tao?
    options.ClientSecret = "901564A5-E7FE-42CB-B10D-61EF6A8F3654";
    
    // 3. Quy trình xin cấp quyền là kiểu gì? Đáp: Kiểu Cấp Code (Authorization Code Flow)
    // Nghĩa là: Tôi chỉ xin 1 tấm Mã Xé (Code), sau đó Đóng Cửa Lại Từ Backend Tôi Gửi Lên Ông Lấy Token Chứ Ko Xin Thẳng. (Tránh bị soi lén trên Trình Duyệt).
    options.ResponseType = "code";
    
    // Dành cho phát triển ở Local. (Nếu bật thành true nó bắt HTTPS mới chịu chạy)
    options.RequireHttpsMetadata = false;

    // QUAN TRỌNG: Cầu xin Middleware giấu giùm Tấm Thẻ "Access Token" nhận được nhét luôn vào Cookie cho tôi nhờ!
    // (Lát nữa tôi đào nó lên xài để xin Data)
    options.SaveTokens = true;

    // Mình xin xỏ (Scopes) những hồ sơ nào? Nếu thằng AuthServer Cấp Đủ thì mình ghi đè những thông tin nhận được đó vào Máy chủ này.
    options.Scope.Add("api1");    // Xin quyền truy cập mã api1
    options.Scope.Add("profile"); // Xin danh tính Profile
    options.Scope.Add("roles");   // Xin phân quyền Role
    options.Scope.Add("offline_access"); // Xin quyền Refresh Token
});

// Cấu hình một cái HttpClient (Đường Ống Khép Kín) để chút nữa WebClient xài bơm dữ liệu từ cổng 5002 (ResourceAPI) về.
builder.Services.AddHttpClient("ResourceApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5002/");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection(); // Tạm tắt để demo port 5003 qua HTTP
app.UseStaticFiles();

app.UseRouting();

// Cửa bảo vệ bắt buộc phải có cho Web có Login
app.UseAuthentication();
app.UseAuthorization();

// Bật đọc các file Controller (Controller -> View -> HTML)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run("http://localhost:5003");
