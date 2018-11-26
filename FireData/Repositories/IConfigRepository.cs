using ApplicationInterface;
using ApplicationInterface.DataAccess;
using FireWorkforceManagement.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Mvc;

namespace FireWorkforceManagement.Repositories {
    public interface IConfigRepository {
        List<SelectListItem> PlatoonList { get; }
        List<SelectListItem> RankList { get; }
        List<SelectListItem> ApparatusList { get; }
        List<SelectListItem> StationList { get; }
        List<string> SkillList { get; }
        List<GridColumn> FirefighterGridColumns { get; }
        List<DayCode> DayCodeList { get; }
        List<string> WorkingDayCodeList { get; }
        List<Tuple<string, string>> GetPositionList(string apparatus);

        Dictionary<string, string> GetApparatusNotes();
        bool SaveApparatusNotes(string apparatus, string date, string notes, string createdBy);
    }

    public class ConfigRepository : IConfigRepository {
        private readonly string _drConn = ConfigurationManager.ConnectionStrings["dr"]?.ConnectionString;

        private List<DayCode> _dayCodes;
        public List<DayCode> DayCodeList
        {
            get
            {
                if (_dayCodes == null) {
                    _dayCodes = new List<DayCode> { //Color2 is 80% lighter than Color
                        new DayCode {Description="Vacation", Code = "V", Color = "blue", Color2 = "#9999ff", EarningCode = "VAS" },     //vacation
                        new DayCode {Description="Lieu", Code = "L", Color = "green", Color2 = "#99ff99", EarningCode = "STL" },    //Lieu
                        new DayCode {Description="Over time", Code = "OT", Color = "red", Color2 = "#ff9999", EarningCode = "OTF" },     //Over time
                        new DayCode {Description="Sick", Code = "S", Color = "purple", Color2 = "#ff99ff", EarningCode = "SKS" },   //Sick
                        new DayCode {Description="Time Owing", Code = "TO", Color = "grey", Color2 = "#cccccc", EarningCode = "REG" },    //Time Owing
                        new DayCode {Description="Family Day", Code = "FD", Color = "green", Color2 = "#99ff99", EarningCode = "LIEU" },   //Family Day
                        new DayCode {Description="Acting Captain", Code = "AC", Color = "orange", Color2 = "#ffdb99", EarningCode = "ATC" },  //Acting Capitan
                        new DayCode {Description="Acting Platoon Chief", Code = "APC", Color = "orange", Color2 = "#ffdb99", EarningCode = "ATP" }, //Acting Platoon Chief
                        new DayCode {Description="Taking a Day Trade", Code = "DT", Color = "yellow", Color2 = "#ffff99", EarningCode = "No Pay" },  //Taking a Day Trade
                        new DayCode {Description="Working Day Trade", Code = "W", Color = "yellow", Color2 = "#ffff99", EarningCode = "No Pay" },   //Working Day Trade
                        new DayCode {Description="Compensation", Code = "C", Color = "purple", Color2 = "#ff99ff", EarningCode = "WSIB" },   //Compensation
                        new DayCode {Description="Light Duty", Code = "LD", Color = "blue", Color2 = "#9999ff", EarningCode = "REG" },    //Light Duty
                        new DayCode {Description="Acting While on Over", Code = "AOT", Color = "red", Color2 = "#ff9999", EarningCode = "OTF" },    //Acting While on Over
                        new DayCode {Description="Overtime While on Vacation", Code = "OTV", Color = "blue", Color2 = "#9999ff", EarningCode = "OTF" },   //Overtime While on Vacation
                        new DayCode {Description="Overtime while on Lieu", Code = "OTL", Color = "green", Color2 = "#99ff99", EarningCode = "OTF" },  //Overtime while on Lieu
                        new DayCode {Description="Sick on Day Trade", Code = "SDT", Color = "purple", Color2 = "#ff99ff", EarningCode = "No Pay" }, //Sick on Day Trade
                        new DayCode {Description="Partial Vacation", Code = "PV", Color = "blue", Color2 = "#9999ff", EarningCode = "VAS" },    //Partial Vacation
                        new DayCode {Description="Partial Lieu", Code = "PL", Color = "green", Color2 = "#99ff99", EarningCode = "STL" },   //Partial Lieu
                        new DayCode {Description="Partial Overtime", Code = "POT", Color = "red", Color2 = "#ff9999", EarningCode = "OTF" },    //Partial Overtime
                        new DayCode { Description = "Half Day Overtime - anything 12hrs and above", Code = "HOT", Color = "red", Color2 = "#ff9999", EarningCode = "OTF" },    //Half Day Overtime - anything 12hrs and above
                        new DayCode { Description="Half Day Acting While on Over Time", Code = "AHOT", Color = "red", Color2 = "#ff9999", EarningCode = "OTF" },   //Half Day Acting While on Over Time                 ",    "
                        new DayCode { Description="Partial Sick", Code = "PS", Color = "purple", Color2 = "#ff99ff", EarningCode = "SKS" },  //Partial Sick
                        new DayCode { Description="Partial Special Leave", Code = "PSL", Color = "brown", Color2 = "#eaaeae", EarningCode = "REG" },  //Partial Special Leave
                        new DayCode { Description="Partial Time Owing", Code = "PTO", Color = "grey", Color2 = "#cccccc", EarningCode = "REG" },   //Partial Time Owing
                        new DayCode { Description="Partial Acting Capitan", Code = "PAC", Color = "orange", Color2 = "#ffdb99", EarningCode = "ATC" }, //Partial Acting Capitan
                        new DayCode { Description="Partial Day Trade", Code = "PDT", Color = "yellow", Color2 = "#ffff99", EarningCode = "No Pay" }, //Partial Day Trade
                        new DayCode { Description="Half Day Trade - anything 12hrs and above", Code = "HDT", Color = "yellow", Color2 = "#ffff99", EarningCode = "No Pay" },  //Half Day Trade - anything 12hrs and above
                       new DayCode { Description = "Normal Working Day", Code = "NW", Color = "black", Color2 = "#cccccc", EarningCode = "REG"},
                       new DayCode {Description="Ontario Fire College", Code = "O", Color = "green", Color2 = "#99ff99", EarningCode = "REG" },   //Ontario Fire College
                    };
                    _dayCodes = _dayCodes.OrderBy(d => d.Code).ToList();
                }
                return _dayCodes;
            }
        }
        public List<string> WorkingDayCodeList => new List<string> {
            "NW", "OT", "POT", "HOT", "AHOT", "AC", "APC", "DT", "W", "LD", "AOT", "OTV", "OTL", "PAC", "PDT", "HDT"
        };

        private List<GridColumn> _ffGridColumns;
        public List<GridColumn> FirefighterGridColumns
        {
            get
            {
                if (_ffGridColumns == null) {
                    _ffGridColumns = new List<GridColumn> {
                        new GridColumn { field = "FullName", displayName = "Name" },
                        new GridColumn { field = "StartDateStr", displayName = "Start Date" },
                        new GridColumn { field = "Platoon", displayName = "Platoon" },
                        new GridColumn { field = "Rank", displayName = "Rank" },
                        new GridColumn { field = "Apparatus", displayName = "Apparatus" }
                    };
                }
                return _ffGridColumns;
            }
        }
        private List<SelectListItem> _apparatusList;
        public List<SelectListItem> ApparatusList
        {
            get
            {
                if (_apparatusList == null) {
                    _apparatusList = new List<SelectListItem> {
                        new SelectListItem {Text = "Pump 1", Value = "Pump 1" },
                        new SelectListItem {Text = "Aerial 1", Value = "Aerial 1" },
                        new SelectListItem {Text = "Pump 2", Value = "Pump 2" },
                        new SelectListItem {Text = "Pump 3", Value = "Pump 3" },
                        new SelectListItem {Text = "Pump 4", Value = "Pump 4" },
                        new SelectListItem {Text = "Tanker 1", Value = "Tanker 1" },
                        new SelectListItem {Text = "Pump 12", Value = "Pump 12" },
                        new SelectListItem {Text = "Service 1", Value = "Service 1" },
                        new SelectListItem {Text = "Training 1", Value = "Training 1" },
                        new SelectListItem {Text = "Car23", Value="Car23" }
                    };
                }
                return _apparatusList;
            }
        }
        private List<SelectListItem> _platoonList;
        public List<SelectListItem> PlatoonList
        {
            get
            {
                if (_platoonList == null) {
                    //Platoon value must be integer!
                    _platoonList = new List<SelectListItem> {
                        new SelectListItem {Text = "Platoon 1", Value = "1" },
                        new SelectListItem {Text = "Platoon 2", Value = "2" },
                        new SelectListItem {Text = "Platoon 3", Value = "3" },
                        new SelectListItem {Text = "Platoon 4", Value = "4" }
                    };
                }
                return _platoonList;
            }
        }
        private List<SelectListItem> _rankList;
        public List<SelectListItem> RankList
        {
            get
            {
                if (_rankList == null) {
                    _rankList = new List<SelectListItem> {
                        new SelectListItem { Text = "Platoon Chief", Value = "PC" },  //Platoon Chief
                        new SelectListItem { Text = "Fire Fighter", Value = "FF" },  //Fire Fighter
                        new SelectListItem { Text = "Probationary FF", Value = "P" },  //Probationary Fire Fighter
                        new SelectListItem { Text = "Acting Captain", Value = "AC" },  //Acting Captain
                        new SelectListItem { Text = "Captain", Value = "C" },  //Captain
                        new SelectListItem { Text = "Senior Captain", Value = "SC" },  //Captain
                        new SelectListItem { Text = "Captain Training Officer", Value = "CTO" },  //Captain Training Officer
                        new SelectListItem { Text = "Chief Mechanic", Value = "CM" },  //Chief Mechanic
                        new SelectListItem { Text = "Mechanic/Acting Captain", Value = "MAC" },  //Mechanic/Acting Captain
                        new SelectListItem { Text = "Mechanic/Fire Fighter", Value = "MFF" },  //Mechanic/Fire Fighter
                        new SelectListItem { Text = "Acting Platoon Chief", Value = "APC" },  //Acting Platoon Chief
                        
                    };
                }
                return _rankList;
            }
        }
        private List<string> _skillList;
        public List<string> SkillList
        {
            get
            {
                if (_skillList == null) {
                    _skillList = new List<string> {
                        "Platoon Chief", "Officer", "Driver","Hydrant","Hose", "Aerial"
                    };
                }
                return _skillList;
            }
        }

        public List<SelectListItem> StationList
        {
            get
            {
                return new List<SelectListItem> {
                    new SelectListItem {Text = "Station 1", Value = "Station 1" },
                    new SelectListItem {Text = "Station 2", Value = "Station 2" },
                    new SelectListItem {Text = "Station 3", Value = "Station 3" },
                    new SelectListItem {Text = "Station 4", Value = "Station 4" }
                };
            }
        }

        public List<Tuple<string, string>> GetPositionList(string apparatus) {
            if (apparatus.Contains("Pump") || apparatus.Contains("Aerial")) {
                return new List<Tuple<string, string>> {
                    new Tuple<string, string>("Officer", "Officer"),
                    new Tuple<string, string>("Driver", "Driver"),
                    new Tuple<string, string>("Hydrant", "Hydrant"),
                    new Tuple<string, string>("Hose", "Hose"),
                    new Tuple<string, string>("Hose 2", "Hose")
                };
            }
            if (string.Equals("Car23", apparatus, StringComparison.OrdinalIgnoreCase)) {
                return new List<Tuple<string, string>> {
                    new Tuple<string, string>("Platoon Chief", "Platoon Chief")
                };
            }
            return new List<Tuple<string, string>>();
        }

        public Dictionary<string, string> GetApparatusNotes() {
            var ret = ApparatusList.Select(l => new { Key = l.Value, Value = string.Empty }).ToDictionary(k => k.Key, v => v.Value);

            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand($"SELECT APPARATUS, NOTES FROM APPARATUS_NOTES WHERE {OracleHelper.MakeSqlWhereString("DATE_STAMP", DateTime.Now.ToString("yyyy-MM-dd"))}", conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                    while (reader.Read()) {
                        var loc = reader.GetSafeString(0);
                        if (ret.ContainsKey(loc)) {
                            ret[loc] = reader.GetSafeString(1);
                        }
                    }
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error retrieving apparatus notes. {ex.Message}");
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }

            return ret;
        }

        public bool SaveApparatusNotes(string apparatus, string date, string notes, string createdBy) {
            using (var conn = new OracleConnection(_drConn)) {
                conn.Open();
                var trans = conn.BeginTransaction();
                try {
                    var cmd_0 = new OracleCommand($"SELECT COUNT(NOTES) FROM APPARATUS_NOTES WHERE {OracleHelper.MakeSqlWhereString("DATE_STAMP", date)} AND {OracleHelper.MakeSqlWhereString("APPARATUS", apparatus)}", conn);
                    var count = Convert.ToInt32(cmd_0.ExecuteScalar());
                    var sql = count > 0 ? $"UPDATE APPARATUS_NOTES SET NOTES={OracleHelper.MakeSqlString(notes)} WHERE {OracleHelper.MakeSqlWhereString("DATE_STAMP", DateTime.Now.ToString("yyyy-MM-dd"))} AND {OracleHelper.MakeSqlWhereString("APPARATUS", apparatus)}" : $"INSERT INTO APPARATUS_NOTES (APPARATUS, DATE_STAMP, NOTES, CREATED_BY) VALUES({OracleHelper.MakeSqlString(apparatus)}, '{date}', {OracleHelper.MakeSqlString(notes)}, '{createdBy}')";

                    var cmd = new OracleCommand(sql, conn);
                    cmd.ExecuteNonQuery();

                    trans.Commit();

                    return true;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error saving apparatus notes. {ex.Message}");
                    trans.Rollback();
                    return false;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }
    }
}
