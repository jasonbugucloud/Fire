using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Mvc;

namespace FireWorkforceManagement.Models {
    public class DailyRoster {
        public string Apparatus { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string Position { get; set; }
        public string Skill { get; set; }
        public List<DailyRosterSchedule> Allocations { get; set; } = new List<DailyRosterSchedule>();
    }

    public class DailyRosterSchedule {
        public int WorkHourId { get; set; }
        public int FirefighterId { get; set; }
        public string UserName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get { return $"{FirstName} {LastName}"; } }
        public string DayCode { get; set; }
        public string Skills { get; set; }
        public DateTime StartsAt { get; set; }
        public string StartsAtStr { get { return StartsAt.ToString("MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture); } }
        public DateTime EndsAt { get; set; }
        public string EndsAtStr { get { return EndsAt.ToString("MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture); } }
        public bool? CheckedIn { get; set; }
        public string Apparatus { get; set; }
        public string Platoon { get; set; }
    }

    public class DailyRosterDashboard {
        public SelectListItem Apparatus { get; set; }
        public List<Roster> RosterList { get; set; }
    }
    public class Roster {
        public string Position { get; set; }
        public List<DailyRosterSchedule> Allocations { get; set; } = new List<DailyRosterSchedule>();
    }
}