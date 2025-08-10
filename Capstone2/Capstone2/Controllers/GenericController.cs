using Capstone2.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Capstone2.Controllers
{
    public class GenericController : Controller
    {
        public int userId;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!HttpContext.Session.GetInt32("UserId").HasValue)
            {
                context.Result = RedirectToAction("Login", "Home");
                return;
            }

            userId = int.Parse(HttpContext.Session.GetInt32("UserId").Value.ToString());
            base.OnActionExecuting(context);
        }
    }
}
