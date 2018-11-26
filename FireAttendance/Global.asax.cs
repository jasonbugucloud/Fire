using ApplicationInterface.Models;
using ApplicationInterface.Repositories;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace FireAttendance {
    public class MvcApplication : System.Web.HttpApplication {
#if DEBUG
        public static bool isDebugMode = true;
#else
        public static bool isDebugMode = false;
#endif

        protected void Application_Start() {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            Bootstrapper.Initialize();
        }
    }
    public class BaseController : Controller {
        private IUserRepository _userRepository;
        public BaseController(IUserRepository repo) {
            _userRepository = repo;
        }
        public PermissionLevelEnum UserPermission { get; set; } = PermissionLevelEnum.None;
        protected override void OnActionExecuting(ActionExecutingContext filterContext) {
            if (filterContext.HttpContext.User is UserProfile) {
                ViewBag.LoginName = (filterContext.HttpContext.User as UserProfile).FullName;
                //All login users have access
                UserPermission = PermissionLevelEnum.User;
            }
            else if (!filterContext.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), false)) {
                filterContext.Result = new HttpUnauthorizedResult();
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult SignOut() {
            Response.ClearContent();
            Response.ClearHeaders();
            return View();
        }
    }

}
