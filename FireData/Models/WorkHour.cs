using System;
using System.Collections.Generic;

namespace FireWorkforceManagement.Models {
    public class WorkHour {
        public int Id { get; set; } = -1;
        public int FireFighterId { get; set; }
        public string DayCode { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public Recurrence Recur { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName => $"{FirstName} {LastName}";
        public string Note { get; set; }
    }

    public class Recurrence {
        public int Id { get; set; } = -1;
        public string Type { get; set; } //DAILY or WEEKLY
        public List<DayOfWeek> WeekDays { get; set; } = new List<DayOfWeek>();
        public DateTime Effective { get; set; }
        public DateTime Until { get; set; }
    }

    public class WorkHourSimpleModel {
        public string FullName { get; set; }
        public string DayCode { get; set; }
        public string Color { get; set; }
    }
}