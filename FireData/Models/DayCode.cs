using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FireWorkforceManagement.Models {
    public class DayCode {
        public string Code { get; set; }
        /// <summary>
        /// Must be web-recognized color, e.g. red or #cccccc
        /// </summary>
        public string Color { get; set; }
        public string Color2 { get; set; }
        public string Description { get; set; }
        public string EarningCode { get; set; }
    }
}