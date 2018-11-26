using System;
using System.Collections.Generic;

namespace FireWorkforceManagement.Models {
    public class Attendance : ICloneable {
        private string _dateString;
        private DateTime _date;
        public string DateString { get { return _dateString; } }
        public List<DayCode> Codes { get; set; } = new List<DayCode>();
        public DateTime Date
        {
            get
            {
                return _date;
            }
            set
            {
                _date = value;
                _dateString = value.ToString("dd ddd");
            }
        }

        public object Clone() {
            return new Attendance {
                Codes = Codes,
                Date = Date
            };
        }
    }

    public class Attendances {
        public string Name { get; set; }
        public List<Attendance> Records { get; set; }
    }
}