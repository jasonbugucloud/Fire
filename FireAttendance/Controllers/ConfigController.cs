using ApplicationInterface.Repositories;
using FireAttendance.Helpers;
using FireWorkforceManagement.Models;
using FireWorkforceManagement.Repositories;
using System.Collections.Generic;
using System.Web.Mvc;

namespace FireAttendance.Controllers {
    public class ConfigController : BaseController {
        private IConfigRepository _configRepo;
        public ConfigController(IUserRepository repo, IConfigRepository repo2) : base(repo) {
            _configRepo = repo2;
        }

        public JsonResult GetDayCodeList() {
            return Json(_configRepo.DayCodeList, JsonRequestBehavior.AllowGet);
        }

    }
}