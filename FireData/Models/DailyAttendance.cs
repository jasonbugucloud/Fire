using System;
using System.Collections.Generic;

namespace FireWorkforceManagement.Models {
    public class DailyAttendance {
        public string Platoon { get; set; }
        public string PlatoonText { get; set; }
        public DateTime Date { get; set; }
        public List<Tuple<string, List<DailyAttendanceLocationData>>> AttendanceList { get; set; }
        public List<WorkHour> OffDutyList { get; set; }
        public int OnDutyStaff { get; set; }
    }

    public class DailyAttendanceLocationData {
        public string Position { get; set; }
        public string Name { get; set; }
        public string DayCode { get; set; }
        public string Time { get; set; }
        public string Replacement { get; set; }
        public string Platoon { get; set; }
        public string ReplaceDayCode { get; set; }
        public string ReplaceTime { get; set; }
    }
}