using ApplicationInterface.Models;
using System.Web.Mvc;

namespace FireAttendance {
    public class FilterConfig {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters) {
            filters.Add(new BasicAuthenticationAttribute());
            filters.Add(new HandleErrorAttribute());
        }
    }

    public class BasicAuthenticationAttribute : AuthorizeAttribute {
        public override void OnAuthorization(AuthorizationContext filterContext) {
            if (filterContext.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), false)) return;

            if (filterContext.HttpContext.User is UserProfile) return;

            UserProfile user = UserProfile.fromHttpContext(filterContext.HttpContext.ApplicationInstance.Context);
            if (user != null) {
                filterContext.HttpContext.User = user;
            }
        }
    }
}
