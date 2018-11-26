using ApplicationInterface.Models;
using ApplicationInterface.Repositories;
using FireData.Models;
using FireData.Repositories;
using FireWorkforceManagement.Repositories;
using System.Web.Mvc;

namespace FireAttendance.Controllers {
    public class HomeController : BaseController {
        private IFireFightersRepository _ffRepo;
        private IWorkHourRepository _whRepo;
        private IDailyRosterRepository _drRepo;
        private ITimeOwingRepository _toRepo;
        private IOverTimeRepository _otRepo;
        public HomeController(IUserRepository repo, IDailyRosterRepository repo2, IFireFightersRepository repo3, IWorkHourRepository repo4, ITimeOwingRepository repo5, IOverTimeRepository repo6) : base(repo) {
            _drRepo = repo2;
            _ffRepo = repo3;
            _whRepo = repo4;
            _toRepo = repo5;
            _otRepo = repo6;
        }
        public ActionResult Index() {
            ViewBag.Title = "Fire Team Attendance";
            return View();
        }

        public JsonResult DailyRoster(string date) {
            return Json(_drRepo.GetDailyRosterDashboardList(date), JsonRequestBehavior.AllowGet);
        }

        public JsonResult Attendance(int year, int month) {
            if (User is UserProfile) {
                var firefighterId = _ffRepo.GetIdByUsername(((UserProfile)User).UserName);
                if (firefighterId == -1) {
                    return Json(null, JsonRequestBehavior.AllowGet);
                }
                return Json(new { Attendances = _whRepo.GetByMonth(firefighterId, year, month) }, JsonRequestBehavior.AllowGet);
            }
            return Json(null, JsonRequestBehavior.AllowGet);
        }

        public JsonResult RequestTimeOwing(TimeOwingModel model) {
            if (User is UserProfile) {
                var firefighterId = _ffRepo.GetIdByUsername(((UserProfile)User).UserName);
                if (firefighterId == -1) {
                    return Json(new { success = false, message = "You are not fire fighter." }, JsonRequestBehavior.AllowGet);
                }
                model.FirefighterId = firefighterId;
                int newId;
                return Json(new { success = _toRepo.Request(model, out newId), message = _toRepo.ErrorMessage, id = newId }, JsonRequestBehavior.AllowGet);
            }
            return Json(null, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetTimeOwingList() {
            if (User is UserProfile) {
                var firefighterId = _ffRepo.GetIdByUsername(((UserProfile)User).UserName);
                if (firefighterId == -1) {
                    return Json(null, JsonRequestBehavior.AllowGet);
                }
                return Json(_toRepo.Get(firefighterId), JsonRequestBehavior.AllowGet);
            }
            return Json(null, JsonRequestBehavior.AllowGet);
        }

        public JsonResult DeleteTimeOwing(int Id) {
            if (User is UserProfile) {
                var firefighterId = _ffRepo.GetIdByUsername(((UserProfile)User).UserName);
                if (firefighterId == -1) {
                    return Json(null, JsonRequestBehavior.AllowGet);
                }
                var ret = _toRepo.Remove(firefighterId, Id);
                return Json(new { success = ret, message = ret ? "Time Owing request deleted." : _toRepo.ErrorMessage }, JsonRequestBehavior.AllowGet);
            }
            return Json(null, JsonRequestBehavior.AllowGet);
        }

        public JsonResult UpdateTimeOwing(TimeOwingModel model) {
            if (User is UserProfile) {
                var firefighterId = _ffRepo.GetIdByUsername(((UserProfile)User).UserName);
                if (firefighterId == -1) {
                    return Json(new { success = false, message = "You are not fire fighter." }, JsonRequestBehavior.AllowGet);
                }
                return Json(new { success = _toRepo.Modify(firefighterId, model), message = _toRepo.ErrorMessage }, JsonRequestBehavior.AllowGet);
            }
            return Json(null, JsonRequestBehavior.AllowGet);
        }
        public JsonResult RequestOvertime(OverTimeModel model) {
            if (User is UserProfile) {
                var firefighterId = _ffRepo.GetIdByUsername(((UserProfile)User).UserName);
                if (firefighterId == -1) {
                    return Json(new { success = false, message = "You are not fire fighter." }, JsonRequestBehavior.AllowGet);
                }
                model.FirefighterId = firefighterId;
                int newId;
                return Json(new { success = _otRepo.Request(model, out newId), message = _otRepo.ErrorMessage, id = newId }, JsonRequestBehavior.AllowGet);
            }
            return Json(null, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetOverTimeList() {
            if (User is UserProfile) {
                var firefighterId = _ffRepo.GetIdByUsername(((UserProfile)User).UserName);
                if (firefighterId == -1) {
                    return Json(null, JsonRequestBehavior.AllowGet);
                }
                return Json(_otRepo.Get(firefighterId), JsonRequestBehavior.AllowGet);
            }
            return Json(null, JsonRequestBehavior.AllowGet);
        }
        public JsonResult DeleteOvertime(int Id) {
            if (User is UserProfile) {
                var firefighterId = _ffRepo.GetIdByUsername(((UserProfile)User).UserName);
                if (firefighterId == -1) {
                    return Json(null, JsonRequestBehavior.AllowGet);
                }
                var ret = _otRepo.Remove(firefighterId, Id);
                return Json(new { success = ret, message = ret ? "Overtime request deleted." : _otRepo.ErrorMessage }, JsonRequestBehavior.AllowGet);
            }
            return Json(null, JsonRequestBehavior.AllowGet);
        }
        public JsonResult UpdateOvertime(OverTimeModel model) {
            if (User is UserProfile) {
                var firefighterId = _ffRepo.GetIdByUsername(((UserProfile)User).UserName);
                if (firefighterId == -1) {
                    return Json(new { success = false, message = "You are not fire fighter." }, JsonRequestBehavior.AllowGet);
                }
                return Json(new { success = _otRepo.Modify(firefighterId, model), message = _otRepo.ErrorMessage }, JsonRequestBehavior.AllowGet);
            }
            return Json(null, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetNotification() {
            if (User is UserProfile) {
                var firefighterId = _ffRepo.GetIdByUsername(((UserProfile)User).UserName);
                if (firefighterId == -1) {
                    return Json(null, JsonRequestBehavior.AllowGet);
                }
                return Json(new { notifyTo = _toRepo.GetNotification(firefighterId), notifyOt = _otRepo.GetNotification(firefighterId) }, JsonRequestBehavior.AllowGet);
            }
            return Json(null, JsonRequestBehavior.AllowGet);
        }
        public JsonResult SetNotified() {
            if (User is UserProfile) {
                var firefighterId = _ffRepo.GetIdByUsername(((UserProfile)User).UserName);
                if (firefighterId != -1) {
                    _toRepo.SetNotified(firefighterId);
                    _otRepo.SetNotified(firefighterId);

                    return Json(true, JsonRequestBehavior.AllowGet);
                }
            }
            return Json(null, JsonRequestBehavior.AllowGet);
        }
    }
}