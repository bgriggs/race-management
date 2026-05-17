using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;


[Route("v{version:apiVersion}/[controller]/[action]")]
[ApiVersion("1.0")]
public class ConfigurationController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
