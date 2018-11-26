using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace FireWorkforceManagement.Models {
    public class FireFighter {
        [Column("ID")]
        public int Id { get; set; }
        [Column("FIRST_NAME")]
        public string FirstName { get; set; }
        [Column("LAST_NAME")]
        public string LastName { get; set; }
        public string FullName { get { return $"{FirstName} {LastName}"; } }
        [Column("USER_NAME")]
        public string UserName { get; set; }
        [Column("START_DATE")]
        public DateTime StartDate { get; set; }
        public string StartDateStr { get { return StartDate.ToString("yyyy-MM-dd"); } }
        [Column("PHONE")]
        public string PhoneNumber { get; set; }
        [Column("CELL_PHONE")]
        public string CellNumber { get; set; }
        [Column("PLATOON")]
        public string Platoon { get; set; }
        public string PlatoonText { get; set; }
        [Column("RANK")]
        public string Rank { get; set; }
        public string RankText { get; set; }
        [Column("APPARATUS")]
        public string Apparatus { get; set; }
        public string ApparatusText { get; set; }
        [Column("NOTE")]
        public string Notes { get; set; }
        [Column("ACTIVE")]
        public bool Active { get; set; } = true;
        public string Status { get { return Active ? "Active" : "Deactivated"; } }
        public List<string> Skills { get; set; } = new List<string>();
        public string SkillsStr { get { return string.Join(", ", Skills); } }
        [Column("STATION")]
        public string Station { get; set; }
    }

    public class FireFighterSearch {
        public string LastName { get; set; }
        public string Platoon { get; set; }
        public string Rank { get; set; }
        public string Apparatus { get; set; }
        public bool ActiveOnly { get; set; }
    }
}