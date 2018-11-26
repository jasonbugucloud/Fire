using System.Web.Optimization;

namespace FireAttendance {
    public class BundleConfig {
        public static void RegisterBundles(BundleCollection bundles) {
            bundles.Add(new ScriptBundle("~/bundles/j").Include(
                        "~/Scripts/jquery-{version}.js"));
            bundles.Add(new ScriptBundle("~/bundles/m").Include(
                        "~/Scripts/modernizr-*"));
            bundles.Add(new ScriptBundle("~/bundles/a").Include(
                    "~/Scripts/angular.js", "~/Scripts/angular-route.js", "~/Scripts/angular-messages.js",
                    "~/Scripts/angular-animate.js", "~/Scripts/angular-aria/angular-aria.js",
                    "~/Scripts/angular-material/angular-material.js", "~/Scripts/moment.js", "~/Scripts/angular-moment-picker.js"));
            bundles.Add(new ScriptBundle("~/bundles/s").Include("~/Scripts/svg4everybody.js", "~/Scripts/site.js"));
            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/bootstrap.css",
                      "~/Content/angular-material.css", "~/Content/angular-material.layouts.css",
                      "~/Content/angular-material.layout-attributes.css", "~/Content/angular-moment-picker.css",
                      "~/Content/Site.css"));
        }
    }
}
