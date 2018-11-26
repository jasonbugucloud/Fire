using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FireData.Models {
    public class YearlyRosterModel {
        public int Year { get; set; }
        public List<Dictionary<string, string>> Data { get; set; }
    }
}
