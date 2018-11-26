using System.Collections.Generic;

namespace FireWorkforceManagement.Models {
    public class WorkHoursReportModel {
        public string Platoon { get; set; }
        public string Name { get; set; }
        public string DayCode { get; set; }
        public string From { get; set; }
        public string To { get; set; }

        public List<WorkHoursDetailReportModel> Detail { get; set; } = new List<WorkHoursDetailReportModel>();
    }

    public class WorkHoursDetailReportModel {
        public string Platoon { get; set; }
        public string Name { get; set; }
        public string DayCode { get; set; }
        public int Hours { get; set; }
        public string Start { get; set; }
        public string End { get; set; }
        public string Day { get; set; }
    }
}
