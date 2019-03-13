using Microsoft.AspNetCore.Mvc;

namespace ComponentManagerAPI.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View("~/Views/Application/Index.cshtml");
        }
    }
}
