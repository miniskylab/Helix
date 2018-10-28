using Microsoft.AspNetCore.Mvc;

namespace Gui
{
    public abstract class HomeController : Controller
    {
        public string Index() { return "This is Test GET"; }
    }
}