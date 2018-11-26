using System;

namespace FireData.Models {
    public class OverTimeModel {
        public int Id { get; set; }
        public int FirefighterId { get; set; }
        public string Name { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public double Hours => (EndAt - StartAt).TotalHours;
        public OverTimeReason Reason { get; set; }
        public string Explanation { get; set; }
        public DateTime Requested { get; set; }
        public DateTime Approved { get; set; }
        public string ApprovedBy { get; set; }
    }

    public enum OverTimeReason {
        Overtime = 0,
        PartialActingPay = 1
    }
}
