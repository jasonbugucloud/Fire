using System.Collections.Generic;

namespace FireData.Models {
    public class CommitteeMembershipModel {
        public string Committee { get; set; }
        public string Position { get; set; }
        public List<string> FireFighters { get; set; }
    }

    public class OrganizationChartModel
    {
        public List<string> CommitteeList { get; set; }
        public List<CommitteeMembershipModel> MembershipList { get; set; }
    }
}
