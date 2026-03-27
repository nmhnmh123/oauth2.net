using System;
using System.Threading;
using System.Threading.Tasks;
using AuthServer.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;

namespace AuthServer.HostedServices
{
    // HostedService là các tiến trình chạy ngầm khi Ứng dụng .NET (Web) khởi động.
    // Ở class này, khi ứng dụng bật lên, ta chèn dữ liệu khởi tạo mặc định cho ứng dụng để thuận tiện test
    public class TestDataHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public TestDataHostedService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        // Hàm này tự động chạy khi hệ thống Start
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Tạo 1 luồng xử lý mới tạm thời
            using var scope = _serviceProvider.CreateScope();

            // 1. Chắc chắn rằng Database in-memory đã được tạo
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);

            // Seed Users
            if (!context.Users.Any())
            {
                context.Users.Add(new AppUser { Username = "admin", Password = "123", Role = "Admin" });
                context.Users.Add(new AppUser { Username = "user", Password = "123", Role = "User" });
                await context.SaveChangesAsync(cancellationToken);
            }

            // 2. Lấy bộ quản lý Application của OpenIddict (Giúp quản lý thông tin các App Client bên thứ 3)
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            // 3. Nếu kiểm tra trong DB chưa có ứng dụng nào mang tên clientId là 'web-client'
            if (await manager.FindByClientIdAsync("web-client", cancellationToken) is null)
            {
                // Thì tiến hành tạo dòng Client mới trong Database
                await manager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    // Mã định danh của phần mềm bên thứ 3
                    ClientId = "web-client",
                    
                    // Chìa khóa bí mật (Chỉ có AuthServer và Thằng Backend của WebClient được phép biết mật khẩu này!)
                    ClientSecret = "901564A5-E7FE-42CB-B10D-61EF6A8F3654",
                    
                    // Implicit nghĩa là người dùng Đăng Nhập xong là AuthServer tự Đồng Ý (cấp token) luôn
                    // Nếu là Explicit, thì WebClient chuyển qua AuthServer, Login xong, còn bị bật lên thêm 1 bảng hỏi: 
                    // "Bạn có đồng ý cho thằng WebClient đọc thông tin hồ sơ của bạn không? Bấm Có / Không"
                    ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
                    
                    DisplayName = "Web Client Application",
                    
                    // Đây là ĐẶC BIỆT CỐT LÕI CỦA BẢO MẬT OAUTH2:
                    // AuthServer chỉ được phép trả mã Code/Token vào đúng cái đường dẫn này của WebClient thôi.
                    // Nếu Hacker cấu kết lừa AuthServer nhả token vô link của Hacker, AuthServer sẽ kiểm tra và từ chối liền
                    // vì cái link đó ko nằm trong danh sách RedirectUris hợp lệ này!
                    RedirectUris =
                    {
                        new Uri("http://localhost:5003/signin-oidc")
                    },
                    PostLogoutRedirectUris =
                    {
                        new Uri("http://localhost:5003/signout-callback-oidc")
                    },
                    
                    // Định nghĩa rằng: Thằng WebClient này được phép gọi đến các loại URL nào, dùng Luồng gì, và lấy Quyền gì.
                    Permissions =
                    {
                        // Cho phép gọi vào 3 cổng: Mở khóa, Trả Token, và Thoát
                        OpenIddictConstants.Permissions.Endpoints.Authorization,
                        OpenIddictConstants.Permissions.Endpoints.EndSession,
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        
                        // Cấp luồng Authorization Code và Refresh Token
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                        OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                        OpenIddictConstants.Permissions.ResponseTypes.Code,
                        
                        // Cấp cho nó quyền xin xỏ Thông tin cá nhân cơ bản và cả quyền API tự cắt nghĩa ("api1")
                        OpenIddictConstants.Permissions.Prefixes.Scope + "openid", // Cấp quyền OpenID Connect
                        OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access", // Cấp quyền Refresh Token
                        OpenIddictConstants.Permissions.Scopes.Email,
                        OpenIddictConstants.Permissions.Scopes.Profile,
                        OpenIddictConstants.Permissions.Scopes.Roles,
                        OpenIddictConstants.Permissions.Prefixes.Scope + "api1" // Tức là "scope:api1"
                    }
                }, cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
