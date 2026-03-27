using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AuthServer.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Controllers
{
    // Controller đảm nhiệm phần Giao diện Đăng nhập bằng tay của Khách Hàng
    // Không liên quan gì tới Token. Ở đây thuần túy là ASP.NET Core MVC Cookie
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Khi truy cập /Account/Login, nó hiện form HTML
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // returnUrl chứa cái link cũ (Thường là link /connect/authorize)
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // Khi bấm nút SUBMIT trên form
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            // Lấy user từ Database
            var user = _context.Users.FirstOrDefault(u => u.Username == username && u.Password == password);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không đúng.");
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }
            
            // Nếu Mật Khẩu Đúng, ta ghi lại các thông tin của người dùng (Claims)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, $"{user.Username}@example.com"),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role) // Gán Role cho người dùng
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // Hành động Lưu bộ chứng chỉ này xuống trình duyệt (dưới dạng Cookie)
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // Sau khi Lưu xong, NẾU NGƯỜI NÀY DO CHUYỂN HƯỚNG TỪ MỘT NƠI KHÁC SANG (như /connect/authorize...)
            // Thì hãy trả họ (Redirect) quay về nơi đó để bắt đầu quá trình sinh mã Authorization Code.
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            // Nếu người này rảnh rỗi tự gõ /Account/Login trên thanh URL, thì đăng nhập xong vứt về Trang Lời Chào (Home).
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            // Hành động Đăng xuất: Bôi xóa và tiêu hủy thẻ Cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}
