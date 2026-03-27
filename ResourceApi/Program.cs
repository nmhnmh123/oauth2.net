using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Khởi tạo các API Controller. Không dùng View vì đây thuần túy là cung cấp dữ liệu
builder.Services.AddControllers();

// Đăng ký OpenIddict - Nhưng với vai trò là máy quét thẻ (Validation Server) chứ không phải máy cấp thẻ.
builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        // Nó sẽ "Khám phá" (Discovery) xem "Ủa cái thẻ Token này nãy được phát hành từ chỗ nào?"
        // Hàm SetIssuer chính là chỉ điểm: "Nếu có đứa nào đưa thẻ Token cho mày, hãy gọi điện qua http://localhost:5001/ để hỏi xem thẻ đó là Thật hay Giả".
        options.SetIssuer("http://localhost:5001/");
        
        // Resource này tự nhận tên nó là "ResourceApi". Token nào không cấp cho Audience = "ResourceApi" thì nó không nhận!
        options.AddAudiences("ResourceApi");

        // Bật cơ chế kiểm tra (Introspection) bằng cách gọi thẳng HTTP qua thằng AuthServer (cổng 5001)
        options.UseSystemNetHttp();
        options.UseAspNetCore();
    })
    .AddValidation(options =>
    {
        // Cấu hình để ASP.NET Core hiểu được cái claim "role" ngắn gọn của OpenIddict 
        // thay vì tìm cái link dài ngoằng mặc định của Microsoft.
        options.Configure(o => o.TokenValidationParameters.RoleClaimType = "role");
    });

// Thiết lập chế độ Xác thực (Authentication) cho những Controller nào gắn tag [Authorize]
// Thay vì dùng Cookie (CookieAuthenticationDefaults), Api này dùng "Mặc định của OpenIddict Validation" (Nghĩa là móc trong Header Authorization Bearer ra để đọc).
builder.Services.AddAuthentication(OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);

// Bật rào chắn bảo vệ API
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseRouting();

// Chốt gác chặn đường, Đứa nào qua đường này sẽ bị kiểm tra xem có Authentication (Có Vé Bearer) chưa?
app.UseAuthentication();
app.UseAuthorization();

// Bật chức năng đọc Route các hàm Controller
app.MapControllers();

app.Run("http://localhost:5002");
