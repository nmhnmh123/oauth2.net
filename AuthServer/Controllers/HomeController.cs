using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
