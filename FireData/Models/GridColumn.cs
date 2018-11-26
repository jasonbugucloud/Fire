using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FireWorkforceManagement.Models {
    public class GridColumn {
        public string field { get; set; }
        public string displayName { get; set; }
        public bool enableColumnMenu = false;
    }
}