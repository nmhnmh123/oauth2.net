using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebClient.Controllers
{
    // Trang tính người dùng xem của WebClient. Cổng 5003.
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        // Truyền nhà máy sản xuất (Factory) HttpClient (Ống tiêm dữ liệu) vào controller
        public HomeController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // Trang chủ không bảo vệ, ai vào cũng được
        public IActionResult Index()
        {
            return View();
        }

        // Tấm mộc báo hiệu: TRANG NÀY CHỈ CÓ QUYỀN MỚI ĐƯỢC VÀO!
        // Nếu click vào mà chưa Đăng Nhập (Cookie = Rỗng)
        // Thì Middleware OpenIdConnect lập tức đá đít (redirect) bạn tự động bay sang AuthServer (cổng 5001) để login
        [Authorize]
        public IActionResult SecurePage()
        {
            return View();
        }

        // Hàm Nút Bấm: Thử lấy Token gọi qua API Đang bị khóa của Tài nguyên (Cổng 5002) xem sao
        [Authorize]
        public async Task<IActionResult> CallApi()
        {
            // 1. Đào Mỏ Cookie (Lục lọi Cookie xem có giấu cái access_token mà Tòa Thị Chính 5001 hồi nãy ném lại lúc cấp xong hay không)
            var accessToken = await HttpContext.GetTokenAsync("access_token");

            // 2. Đi ra Nhà Máy Lắp Ráp, mượn 1 Ống Tiêm đã được cài định vị sẵn tới IP Cổng 5002 (Cấu hình bên Program.cs)
            var client = _httpClientFactory.CreateClient("ResourceApi");
            
            // 3. Tút ống tiêm: Gắng cài Tấm Thẻ Bài Access Token vào trên đầu Ống Tiêm (HTTP Authorization Header Bearer)
            if (!string.IsNullOrEmpty(accessToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            // 4. Nhấn cắm tiêm lấy Máu: Gọi qua cổng 5002, xin hàm /api/data.
            var response = await client.GetAsync("/api/data");
            // 5. Nếu bị đuổi vì Token hết hạn (401), Tiến hành tự động lấy Refresh Token đi dổi token mới!
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                var refreshToken = await HttpContext.GetTokenAsync("refresh_token");
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    var refreshClient = _httpClientFactory.CreateClient();
                    var content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "grant_type", "refresh_token" },
                        { "client_id", "web-client" },
                        { "client_secret", "901564A5-E7FE-42CB-B10D-61EF6A8F3654" },
                        { "refresh_token", refreshToken }
                    });

                    var refreshResponse = await refreshClient.PostAsync("http://localhost:5001/connect/token", content);
                    if (refreshResponse.IsSuccessStatusCode)
                    {
                        // Đổi thành công, đọc Token mới ra
                        var json = await refreshResponse.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var newAccessToken = doc.RootElement.GetProperty("access_token").GetString();
                        var newRefreshToken = doc.RootElement.GetProperty("refresh_token").GetString();

                        // Cập nhật lại vào Cookie lưu trên trình duyệt để xài tiếp
                        var authInfo = await HttpContext.AuthenticateAsync("Cookies");
                        authInfo.Properties.UpdateTokenValue("access_token", newAccessToken);
                        authInfo.Properties.UpdateTokenValue("refresh_token", newRefreshToken);
                        await HttpContext.SignInAsync("Cookies", authInfo.Principal, authInfo.Properties);

                        // GỌI LẠI API với mộc truy cập MỚI
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);
                        response = await client.GetAsync("/api/data");
                    }
                }
            }

            // 6. Sau khi refresh (nếu có cần), kiểm tra lại lần cuối
            if (response.IsSuccessStatusCode)
            {
                var contentData = await response.Content.ReadAsStringAsync();
                ViewBag.ApiData = contentData;
            }
            else
            {
                ViewBag.ApiData = $"API Error: Bị Chặn Hoặc Cấm - Lỗi Mã {response.StatusCode} - Sau cả khi thử Refresh.";
            }

            return View("SecurePage");
        }

        // Hàm Đăng Xuất (Xóa cả Cookie của cổng 5003, gửi lệnh Logout chạy ngược lại Cổng 5001 xóa luôn gốc trên AuthServer)
        public IActionResult Logout()
        {
            return SignOut("Cookies", "OpenIdConnect");
        }
    }
}
