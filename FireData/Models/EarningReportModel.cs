namespace FireData.Models {
    public class EarningReportModel {
        public string RunId => string.Empty;
        public int Emplid { get; set; }
        public int SeqNum { get; set; } = 1;
        public string EarnCode { get; set; }
        public string RateCode => string.Empty;
        public double Hours { get; set; } = 0.0;
        public string Rate => string.Empty;
        public string Amount => string.Empty;
        public string FsDept => string.Empty;
        public string Combo => string.Empty;
        public int EmplRcd => 0;
    }
}
