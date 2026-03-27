using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
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
            
            // 5. Nếu cổng 5002 soi Thẻ Bài đúng, nó sẽ trả mã 200 (SuccessStatusCode) và trào máu (json) cho mình.
            if (response.IsSuccessStatusCode)
            {
                // Đọc Máu (Hút Data) bỏ vào ViewBag
                var content = await response.Content.ReadAsStringAsync();
                ViewBag.ApiData = content;
            }
            else
            {
                // Nếu cắm tiêm vô mà nó đuổi ra đánh móp đầu (Thẻ hết hạn, thẻ láo) thì hiện Lỗi
                ViewBag.ApiData = $"API Error: Bị Chặn Hoặc Cấm - Lỗi Mã {response.StatusCode}";
            }

            // Mượn giao diện của trang 1 để hiện lại chứ k qua trang mới
            return View("SecurePage");
        }

        // Hàm Đăng Xuất (Xóa cả Cookie của cổng 5003, gửi lệnh Logout chạy ngược lại Cổng 5001 xóa luôn gốc trên AuthServer)
        public IActionResult Logout()
        {
            return SignOut("Cookies", "OpenIdConnect");
        }
    }
}
