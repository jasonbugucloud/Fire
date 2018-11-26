using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace FireAttendance.Helpers {
    public static class SelectListHelper {
        public static List<SelectListItem> InsertAll(this List<SelectListItem> items, bool insertAll) {
            var ret = insertAll ? new List<SelectListItem> {
                new SelectListItem { Text = "(All)", Value = "*" }
            } : new List<SelectListItem>();
            if (items.Any()) {
                ret.AddRange(items);
            }
            return ret;
        }
    }
}