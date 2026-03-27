using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace AuthServer.Controllers
{
    // Controller này là cốt lõi của OpenIddict.
    // Nơi đây tiếp nhận mọi yêu cầu xin quyền (gọi là Authorization Request) 
    // và điểm đổi mã lấy token (gọi là Token Request) của Client.
    public class AuthorizationController : Controller
    {
        // 1. ENDPOINT: /connect/authorize
        // Được WebClient tự động nhảy sang trang này khi người dùng bấm "Login".
        // Nhiệm vụ của nó: Trả về một "Authorization Code" cho WebClient, NẾU và CHỈ NẾU người dùng đã chịu khó đăng nhập (Login) trên AuthServer.
        [HttpGet("~/connect/authorize")]
        [HttpPost("~/connect/authorize")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Authorize()
        {
            // Lấy thông tin yêu cầu chuẩn OpenID. (Bao gồm client_id, redirect_uri, scopes,...)
            var request = HttpContext.GetOpenIddictServerRequest() ??
                throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            // Kiểm tra xem User này ĐÃ TỰ ĐĂNG NHẬP (Bằng Cookie của trang web này) chưa?
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // NẾU CHƯA ĐĂNG NHẬP:
            if (!result.Succeeded)
            {
                // Gọi hàm "Challenge", về cơ bản là đã ra lệnh cho hệ thống:
                // "Ê, đá thằng User này về trang Đăng nhập (/Account/Login) cho tao! 
                // Khi nào nó gõ đúng Mật Khẩu, thì dắt nó quay lại trang này (RedirectUri) để tao làm việc tiếp!"
                return Challenge(
                    new AuthenticationProperties
                    {
                        RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                            Request.HasFormContentType ? Request.Form.ToList() : Request.Query.ToList())
                    },
                    new[] { CookieAuthenticationDefaults.AuthenticationScheme });
            }

            // NẾU ĐÃ ĐĂNG NHẬP (Result == Succeeded):
            // Lấy các thông tin cơ bản (Tên, Email) chuyển sang dạng thông tin Claim (Đặc tính / Tính chất của người dùng).
            var claims = new List<Claim>
            {
                // Claim.Subject = "Tên ID" bắt buộc phải có của 1 token
                new Claim(OpenIddictConstants.Claims.Subject, result.Principal.Identity.Name),
                
                // Đính kèm Tên vào Access Token (Để ResourceApi đọc được tên mình)
                new Claim(OpenIddictConstants.Claims.Name, result.Principal.Identity.Name)
                    .SetDestinations(OpenIddictConstants.Destinations.AccessToken),
                
                // Đính kèm Email vào Access Token
                new Claim(OpenIddictConstants.Claims.Email, $"{result.Principal.Identity.Name}@example.com")
                    .SetDestinations(OpenIddictConstants.Destinations.AccessToken)
            };

            // Gom rổ Claims này lại thành 1 "Hồ sơ điện tử" định danh bằng giao thức OpenIddict
            var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // QUAN TRỌNG: Cấp phát quyền (Scopes) cho Hồ sơ điện tử này!
            // Khi WebClient xin xỏ "Tôi muốn xem profile và email" -> GetScopes() sẽ có 2 chữ đó.
            // Đoạn Code "SetScopes" này nghĩa là AuthServer DUYỆT / GẬT ĐẦU cho phép WebClient được lấy những quyền đó.
            principal.SetScopes(request.GetScopes());
            
            // Ghi chú rằng Token này chỉ có giá trị khi cầm sang "ResourceApi" xài. 
            // Cầm sang "FaceBookApi" hay "TiktokApi" thì token này bị coi là rác.
            principal.SetResources("ResourceApi");

            // Cuối cùng: Mệnh lệnh SignIn của OpenIddict.
            // Hàm này KHÔNG làm hiện màn hình chờ. Nó tự động tạo ra cái "Authorization Code",
            // rồi bảo trình duyệt (Browser) "Giờ mày lập tức quay trở lại cái link redirect_uri của WebClient đi, cầm theo cái Code này nha".
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // 2. ENDPOINT: /connect/token
        // Đường dẫn này dĩ nhiên NGƯỜI DÙNG BẰNG MẮT THƯỜNG SẼ KHÔNG BAO GIỜ THẤY (Vì nó được Backend của WebClient gọi ngầm HTTP Post).
        // WebClient gửi 1 tấm vé "Authorization Code" lên đây, đổi lấy 1 tấm thẻ thực sự "Access Token".
        [HttpPost("~/connect/token"), Produces("application/json")]
        public async Task<IActionResult> Exchange()
        {
            var request = HttpContext.GetOpenIddictServerRequest() ??
                throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            // Hệ thống kiểm tra xem nó có đang đòi xài cái "Authorization Code" (Hoặc "Refresh Token") để xin việc không.
            if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
            {
                // Authenticate cái tấm vé Code đó. (Hệ thống tự động giải mã tấm vé xem nó có hợp lệ, chưa hết hạn, và chứa gì bên trong)
                var principal = (await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal;

                // Ra mệnh lệnh báo cáo OpenIddict: "Tấm vé Code hợp lệ rồi, phát Token thật sự cho nó đi!"
                // Hàm này sẽ sinh ra 1 cục JSON dài ngoằn (bao gồm Access Token, ID Token, thời gian sống expires_in...) trả về cho Backend của WebClient.
                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            throw new InvalidOperationException("Loại cấp quyền (Grant Type) này không được hỗ trợ.");
        }

        // 3. ENDPOINT: /connect/logout
        // Điểm đăng xuất
        [HttpGet("~/connect/logout")]
        public async Task<IActionResult> Logout()
        {
            // Xóa Cookie Đăng nhập trên trình duyệt
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // OpenIddict sẽ dắt bạn chạy ngược về đường link WebClient (Cổng 5003). 
            return SignOut(
                new AuthenticationProperties
                {
                    RedirectUri = "/"
                },
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
    }
}
