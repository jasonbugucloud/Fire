using System;

namespace FireData.Models {
    public class TimeOwingModel {
        public int Id { get; set; }
        public int FirefighterId { get; set; }
        public string Name { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public double Hours => (EndAt - StartAt).TotalHours;
        public TimeOwingType Type { get; set; }
        public DateTime Requested { get; set; }
        public DateTime Approved { get; set; }
        public string ApprovedBy { get; set; }
    }

    public enum TimeOwingType {
        FamilyDay = 0,
        TimeOwing = 1
    }
}
