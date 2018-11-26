using ApplicationInterface;
using ApplicationInterface.DataAccess;
using FireData.Models;
using FireWorkforceManagement.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace FireWorkforceManagement.Repositories {
    public interface IWorkHourRepository {
        string ErrorMessage { get; }
        List<string> TimeBankDayCodes { get; }
        List<WorkHour> GetByMonth(int firefighterId, DateTime month);
        List<Attendances> GetAttendance(int platoonId, DateTime month, out List<string> days);
        List<WorkHour> GetOffDutyList(int platoonId, DateTime date);
        List<WorkHour> GetByDateRange(int firefighterId, DateTime date1, DateTime date2);
        bool Add(List<WorkHour> whs, out List<WorkHour> newWorkhours);
        bool Update(WorkHour wh);
        bool Remove(int id);
        bool Split(int WorkHourId, DateTime MidPoint, out int newWhId);

        void PopulateWorkHoursReport(WorkHoursReportModel model);
        Dictionary<string, List<WorkHourSimpleModel>> GetAbsence(DateTime date);
        List<WorkHour> GetByMonth(int firefighterId, int year, int month);

        string GetEarningReport(DateTime date1, DateTime date2);
    }

    public class WorkHourRepository : IWorkHourRepository {
        private IConfigRepository _config;
        private IFireFightersRepository _ffRepo;
        private readonly string _drConn = ConfigurationManager.ConnectionStrings["dr"]?.ConnectionString;
        private readonly string _earningReportHeader = "\"Run ID\",\"Emplid\",\"Seq Num\",\"Earn CD\",\"Rate Code\",\"Hours\",\"Rate\",\"Amount\",\"FS Dept\",\"Combo\",\"Empl Rcd\"";
        public string ErrorMessage { get; private set; } = string.Empty;

        public List<string> TimeBankDayCodes => new List<string> { "OT", "AOT", "TO", "FD" };

        public WorkHourRepository(IConfigRepository repo, IFireFightersRepository repo2) {
            _config = repo;
            _ffRepo = repo2;
        }

        public bool Add(List<WorkHour> whs, out List<WorkHour> newWorkhours) {
            ErrorMessage = string.Empty;
            newWorkhours = new List<WorkHour>();
            if (!whs.Any()) {
                ErrorMessage = "No schedule to save.";
                return false;
            }
            var firefighterId = whs.First().FireFighterId;
            if (!_ffRepo.ActiveFirefighterExists(firefighterId)) {
                ErrorMessage = "Firefighter doesn't exist or is not active.";
                return false;
            }
            using (var conn = new OracleConnection(_drConn)) {
                conn.Open();
                var trans = conn.BeginTransaction();
                try {
                    var recur = whs.First().Recur;
                    var ruleId = -1;
                    if (recur != null) {
                        var cmd0 = new OracleCommand($"INSERT INTO RECURRENCES (TYPE, WEEK_DAYS, EFFECTIVE_DATE, UNTIL_DATE) VALUES ({OracleHelper.MakeSqlString(recur.Type)}, {OracleHelper.MakeSqlString(SetString(recur.WeekDays))}, {OracleHelper.MakeSqlString(recur.Effective)}, {OracleHelper.MakeSqlString(recur.Until)})", conn);
                        cmd0.ExecuteNonQuery();

                        var cmd1 = new OracleCommand("SELECT RECURRENCE_ID_SEQ.CURRVAL FROM DUAL", conn);
                        ruleId = Convert.ToInt32(cmd1.ExecuteScalar());
                    }
                    foreach (var wh in whs) {
                        var cmd_pre = new OracleCommand($"SELECT COUNT(*) FROM WORKING_HOURS WHERE {OracleHelper.MakeSqlWhereString("FIREFIGHTER_ID", firefighterId)} AND START_AT < {OracleHelper.MakeSqlString(wh.To)} AND END_AT > {OracleHelper.MakeSqlString(wh.From)}", conn);
                        var countOfExisting = Convert.ToInt32(cmd_pre.ExecuteScalar());
                        if (countOfExisting > 0) {
                            ErrorMessage = "New schedule and existing schedule are overlapped.";
                            trans.Rollback();
                            return false;
                        }

                        var cmd = new OracleCommand($"INSERT INTO WORKING_HOURS (FIREFIGHTER_ID, DAY_CODE, START_AT, END_AT, RECURRENCE_ID, NOTE) VALUES ({OracleHelper.MakeSqlString(wh.FireFighterId)}, {OracleHelper.MakeSqlString(wh.DayCode)}, {OracleHelper.MakeSqlString(wh.From)}, {OracleHelper.MakeSqlString(wh.To)}, {(recur != null ? ruleId.ToString() : "null")}, {OracleHelper.MakeSqlString(wh.Note)})", conn);
                        cmd.ExecuteNonQuery();

                        var cmd_post = new OracleCommand("SELECT WORKING_HOUR_ID_SEQ.CURRVAL FROM DUAL", conn);
                        wh.Id = Convert.ToInt32(cmd_post.ExecuteScalar());
                        if (recur != null) {
                            wh.Recur.Id = ruleId;
                        }
                        newWorkhours.Add(wh);
                    }

                    trans.Commit();
                    return true;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error saving schedule record. {ex.Message}");
                    ErrorMessage = "Failed to save schedule record.";
                    trans.Rollback();
                    newWorkhours = new List<WorkHour>();
                    return false;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }

        public List<WorkHour> GetByMonth(int firefighterId, DateTime month) {
            var date1 = month.AddDays(1 - month.Date.Day);
            var date2 = date1.AddMonths(1);
            return GetByDateRange(firefighterId, date1, date2);
        }
        public List<Attendances> GetAttendance(int platoonId, DateTime month, out List<string> days) {
            var date1 = month.AddDays(1 - month.Date.Day);
            var date2 = date1.AddMonths(1);

            var workHours = GetByDateRange(0 - platoonId, date1, date2);

            days = GetAttendanceList(month.Year, month.Month).Select(a => a.DateString).ToList();

            var ret = new Dictionary<string, List<Attendance>>();
            foreach (var wh in workHours) {
                var name = $"{wh.FirstName} {wh.LastName}";

                if (!ret.ContainsKey(name)) {
                    ret.Add(name, GetAttendanceList(month.Year, month.Month));
                }
                foreach (var att in ret[name]) {
                    if (wh.From.Date == att.Date || wh.To.Date == att.Date) {
                        att.Codes.Add(_config.DayCodeList.FirstOrDefault(c => c.Code == wh.DayCode));
                    }
                }
            }
            return ret.Select(r => new Attendances { Name = r.Key, Records = r.Value }).ToList();
        }
        private List<Attendance> GetAttendanceList(int year, int month) {
            return Enumerable.Range(1, DateTime.DaysInMonth(year, month)).Select(d => new Attendance { Date = new DateTime(year, month, d) }).ToList();
        }
        public List<WorkHour> GetOffDutyList(int platoonId, DateTime date) {
            var from = new DateTime(date.Year, date.Month, date.Day, 7, 0, 0);
            var to = from.AddDays(1);
            return GetByDateRange(0 - platoonId, from, to).Where(w => !_config.WorkingDayCodeList.Contains(w.DayCode)).ToList();
        }
        public List<WorkHour> GetByMonth(int firefighterId, int year, int month) {
            var from = new DateTime(year, month+1, 1, 0, 0, 0);
            var to = from.AddMonths(1);
            var sql = $"SELECT DAY_CODE, START_AT, END_AT FROM WORKING_HOURS WHERE {OracleHelper.MakeSqlWhereString("FIREFIGHTER_ID", firefighterId)}  AND START_AT < {OracleHelper.MakeSqlString(to.Date)} AND END_AT > {OracleHelper.MakeSqlString(from.Date)} ORDER BY START_AT";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                    var ret = new List<WorkHour>();
                    while (reader.Read()) {
                        var wh = new WorkHour {
                            DayCode = reader.GetSafeString(0),
                            From = reader.GetSafeDateTime(1),
                            To = reader.GetSafeDateTime(2)
                        };
                        ret.Add(wh);
                    }
                    return ret;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error retrieving schedule records by {firefighterId}. {ex.Message}");
                    return new List<WorkHour>();
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }
        public List<WorkHour> GetByDateRange(int firefighterId, DateTime date1, DateTime date2) {
            ErrorMessage = string.Empty;
            //if firefighterId is negative, it'd be platoon id.
            var ffCondition = OracleHelper.MakeSqlWhereString("FIREFIGHTER_ID", firefighterId);
            if (firefighterId < 0) {
                ffCondition = OracleHelper.MakeSqlWhereString("FIREFIGHTERS.PLATOON", Math.Abs(firefighterId).ToString());
            }
            var sql = $"SELECT FIREFIGHTER_ID, DAY_CODE, START_AT, END_AT, RECURRENCE_ID, RECURRENCES.TYPE, RECURRENCES.WEEK_DAYS, RECURRENCES.EFFECTIVE_DATE, RECURRENCES.UNTIL_DATE, WORKING_HOURS.ID, FIREFIGHTERS.FIRST_NAME, FIREFIGHTERS.LAST_NAME, WORKING_HOURS.NOTE FROM WORKING_HOURS LEFT OUTER JOIN RECURRENCES ON WORKING_HOURS.RECURRENCE_ID = RECURRENCES.ID LEFT OUTER JOIN FIREFIGHTERS ON FIREFIGHTERS.ID=WORKING_HOURS.FIREFIGHTER_ID WHERE {ffCondition} AND START_AT < {OracleHelper.MakeSqlString(date2.Date)} AND END_AT > {OracleHelper.MakeSqlString(date1.Date)} ORDER BY FIREFIGHTERS.START_DATE";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                    var ret = new List<WorkHour>();
                    while (reader.Read()) {
                        var wh = new WorkHour {
                            FireFighterId = reader.GetSafeInt32(0),
                            DayCode = reader.GetSafeString(1),
                            From = reader.GetSafeDateTime(2),
                            To = reader.GetSafeDateTime(3),
                            Id = reader.GetSafeInt32(9),
                            FirstName = reader.GetSafeString(10),
                            LastName = reader.GetSafeString(11),
                            Note = reader.GetSafeString(12)
                        };
                        if (!reader.IsDBNull(4)) {
                            wh.Recur = new Recurrence {
                                Id = reader.GetSafeInt32(4),
                                Type = reader.GetSafeString(5),
                                WeekDays = GetFromString(reader.GetSafeString(6)),
                                Effective = reader.GetSafeDateTime(7),
                                Until = reader.GetSafeDateTime(8)
                            };
                        }
                        ret.Add(wh);
                    }
                    return ret;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error retrieving schedule records by {firefighterId}. {ex.Message}");
                    ErrorMessage = "Failed to get schedules.";
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
            return new List<WorkHour>();
        }

        public bool Update(WorkHour wh) {
            ErrorMessage = string.Empty;
            if (wh == null) {
                ErrorMessage = "No schedule to save.";
                return false;
            }
            var firefighterId = wh.FireFighterId;
            if (!_ffRepo.ActiveFirefighterExists(firefighterId)) {
                ErrorMessage = "Firefighter doesn't exist or is not active.";
                return false;
            }
            using (var conn = new OracleConnection(_drConn)) {
                conn.Open();
                var trans = conn.BeginTransaction();
                try {
                    var cmd_pre = new OracleCommand($"SELECT ID FROM WORKING_HOURS WHERE {OracleHelper.MakeSqlWhereString("FIREFIGHTER_ID", firefighterId)} AND START_AT < {OracleHelper.MakeSqlString(wh.To)} AND END_AT > {OracleHelper.MakeSqlString(wh.From)}", conn);
                    var existings = cmd_pre.ExecuteReader();
                    while (existings.Read()) {
                        if (existings.GetSafeInt32(0) != wh.Id) {
                            ErrorMessage = "This schedule and another schedule are overlapped.";
                            trans.Rollback();
                            return false;
                        }
                    }
                    var cmd = new OracleCommand($"UPDATE WORKING_HOURS SET DAY_CODE={OracleHelper.MakeSqlString(wh.DayCode)}, START_AT={OracleHelper.MakeSqlString(wh.From)}, END_AT={OracleHelper.MakeSqlString(wh.To)}, NOTE={OracleHelper.MakeSqlString(wh.Note)} WHERE {OracleHelper.MakeSqlWhereString("ID", wh.Id)}", conn);
                    cmd.ExecuteNonQuery();
                    if (!_config.WorkingDayCodeList.Contains(wh.DayCode)) {
                        var cmd_dr = new OracleCommand($"DELETE FROM DAILY_ROSTER WHERE WORKHOUR_ID={wh.Id}", conn);
                        cmd_dr.ExecuteNonQuery();
                    }

                    trans.Commit();
                    return true;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error saving schedule record [ID: {wh.Id}]. {ex.Message}");
                    ErrorMessage = "Failed to save schedule record.";
                    trans.Rollback();
                    return false;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }

        public bool Remove(int id) {
            ErrorMessage = string.Empty;
            if (id < 0) {
                ErrorMessage = "No schedule to remove.";
                return false;
            }
            using (var conn = new OracleConnection(_drConn)) {
                conn.Open();
                var trans = conn.BeginTransaction();
                try {
                    var cmd_pre = new OracleCommand($"SELECT DAY_CODE FROM WORKING_HOURS WHERE ID={id}", conn);
                    if (TimeBankDayCodes.Contains(Convert.ToString(cmd_pre.ExecuteScalar()))){
                        ErrorMessage = "Not allowed to modify work hours from time bank.";
                        trans.Rollback();
                        return false;
                    }
                    var cmd = new OracleCommand($"DELETE FROM WORKING_HOURS WHERE {OracleHelper.MakeSqlWhereString("ID", id)}", conn);
                    cmd.ExecuteNonQuery();
                    var cmd_dr = new OracleCommand($"DELETE FROM DAILY_ROSTER WHERE WORKHOUR_ID={id}", conn);
                    cmd_dr.ExecuteNonQuery();

                    trans.Commit();
                    return true;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error deleting schedule record [ID: {id}]. {ex.Message}");
                    ErrorMessage = "Failed to remove schedule record.";
                    trans.Rollback();
                    return false;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }

        private List<DayOfWeek> GetFromString(string str) {
            if (string.IsNullOrWhiteSpace(str) || str.Length != 7) {
                return new List<DayOfWeek>();
            }
            var ret = new List<DayOfWeek>();
            for (var i = 0; i < 7; i++) {
                if (str[i] == '1') {
                    ret.Add((DayOfWeek)i);
                }
            }
            return ret;
        }
        private string SetString(List<DayOfWeek> weekdays) {
            var ret = new char[] { '0', '0', '0', '0', '0', '0', '0' };
            foreach (var w in weekdays) {
                ret[(int)w] = '1';
            }
            return new string(ret);
        }

        public void PopulateWorkHoursReport(WorkHoursReportModel model) {
            if (model == null) return;

            var sql = $"SELECT DAY_CODE, START_AT, END_AT, FIREFIGHTERS.FIRST_NAME, FIREFIGHTERS.LAST_NAME, FIREFIGHTERS.PLATOON FROM WORKING_HOURS LEFT OUTER JOIN FIREFIGHTERS ON FIREFIGHTERS.ID=WORKING_HOURS.FIREFIGHTER_ID WHERE START_AT < {OracleHelper.MakeSqlString(Convert.ToDateTime(model.To).Date)} AND END_AT > {OracleHelper.MakeSqlString(Convert.ToDateTime(model.From).Date)}";
            var where = new List<string>();
            if (model.Platoon != "*") {
                where.Add($"FIREFIGHTERS.PLATOON={OracleHelper.MakeSqlString(model.Platoon)}");
            }
            else {
                model.Platoon = "(All)";
            }
            if (model.Name != "*") {
                where.Add($"FIREFIGHTERS.ID={OracleHelper.MakeSqlString(model.Name)}");
            }
            if (model.DayCode != "*") {
                where.Add($"DAY_CODE={OracleHelper.MakeSqlString(model.DayCode)}");
            }
            else {
                model.DayCode = "(All)";
            }
            if (where.Any()) {
                sql += " AND " + string.Join(" AND ", where);
            }
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                    var ret = new List<WorkHoursDetailReportModel>();
                    while (reader.Read()) {
                        var startAt = reader.GetSafeDateTime(1);
                        var endAt = reader.GetSafeDateTime(2);
                        ret.Add(new WorkHoursDetailReportModel {
                            Platoon = reader.GetSafeString(5),
                            Name = $"{reader.GetSafeString(3)} {reader.GetSafeString(4)}",
                            Hours = (int)((endAt - startAt).TotalHours + 0.08),
                            Start = startAt.ToString("yyyy-MM-dd HH:mm"),
                            End = endAt.ToString("yyyy-MM-dd HH:mm"),
                            Day = startAt.DayOfWeek.ToString(),
                            DayCode = reader.GetSafeString(0)
                        });
                    }
                    model.Detail = (from r in ret orderby r.Platoon, r.Name, r.DayCode, r.Start select r).ToList();
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error retrieving work hour records. {ex.Message}");
                    ErrorMessage = "Failed to get work hours.";
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }

            }
        }

        public Dictionary<string, List<WorkHourSimpleModel>> GetAbsence(DateTime date) {
            var platoons = _config.PlatoonList;
            var ret = new Dictionary<string, List<WorkHourSimpleModel>>();
            foreach (var platoon in platoons) {
                var offDutyList = GetOffDutyList(Convert.ToInt32(platoon.Value), date);
                ret.Add(platoon.Text, offDutyList.Select(w => new WorkHourSimpleModel {
                    FullName = w.FullName,
                    DayCode = w.DayCode,
                    Color = _config.DayCodeList.FirstOrDefault(d => d.Code == w.DayCode)?.Color ?? "black"
                }).ToList());
            }
            return ret;
        }

        public bool Split(int WorkHourId, DateTime MidPoint, out int newWhId) {
            ErrorMessage = string.Empty;
            newWhId = -1;
            using (var conn = new OracleConnection(_drConn)) {
                conn.Open();
                var trans = conn.BeginTransaction();
                try {
                    var cmd_pre = new OracleCommand($"SELECT FIREFIGHTER_ID, DAY_CODE, END_AT FROM WORKING_HOURS WHERE ID={WorkHourId}", conn);
                    var existing = cmd_pre.ExecuteReader();
                    var wh = new WorkHour();
                    if (existing.Read()) {
                        wh.FireFighterId = existing.GetSafeInt32(0);
                        wh.DayCode = existing.GetSafeString(1);
                        wh.To = existing.GetSafeDateTime(2);
                    }
                    else {
                        ErrorMessage = "Schedule does not exist.";
                        return false;
                    }
                    var cmd = new OracleCommand($"UPDATE WORKING_HOURS SET END_AT={OracleHelper.MakeSqlString(MidPoint)}, NOTE='SPLIT' WHERE {OracleHelper.MakeSqlWhereString("ID", WorkHourId)}", conn);
                    cmd.ExecuteNonQuery();

                    var cmd2 = new OracleCommand($"INSERT INTO WORKING_HOURS (FIREFIGHTER_ID, DAY_CODE, START_AT, END_AT, NOTE) VALUES ({OracleHelper.MakeSqlString(wh.FireFighterId)}, {OracleHelper.MakeSqlString(wh.DayCode)}, {OracleHelper.MakeSqlString(MidPoint)}, {OracleHelper.MakeSqlString(wh.To)}, 'SPLIT')", conn);
                    cmd2.ExecuteNonQuery();

                    var cmd_post = new OracleCommand("SELECT WORKING_HOUR_ID_SEQ.CURRVAL FROM DUAL", conn);
                    newWhId = Convert.ToInt32(cmd_post.ExecuteScalar());

                    trans.Commit();
                    return true;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error saving split schedules. {ex.Message}");
                    ErrorMessage = "Failed to save split schedule records.";
                    trans.Rollback();
                    return false;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }

        public string GetEarningReport(DateTime date1, DateTime date2) {
            var ret = new List<EarningReportModel>();
            var sql = $"SELECT EMP_ID, DAY_CODE, START_AT, END_AT FROM WORKING_HOURS LEFT JOIN FIREFIGHTERS ON WORKING_HOURS.FIREFIGHTER_ID = FIREFIGHTERS.ID WHERE WORKING_HOURS.CHECKED_IN = 'Y' AND START_AT < {OracleHelper.MakeSqlString(date2.Date)} AND END_AT > {OracleHelper.MakeSqlString(date1.Date)}";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                    while (reader.Read()) {
                        var empId = reader.GetSafeInt32(0);
                        var earningCode = (from dc in _config.DayCodeList where string.Equals(dc.Code, reader.GetSafeString(1), StringComparison.OrdinalIgnoreCase) select dc.EarningCode).FirstOrDefault();
                        var startAt = reader.GetSafeDateTime(2);
                        var endAt = reader.GetSafeDateTime(3);
                        var hours = (endAt - startAt).TotalMinutes / 60.0;
                        if (string.Equals(earningCode, "OTF", StringComparison.OrdinalIgnoreCase)) {
                            hours = hours < 1.0 ? 1.0 : hours * 1.5;
                        }
                        if (ret.Any(r => empId == r.Emplid && string.Equals(earningCode, r.EarnCode, StringComparison.OrdinalIgnoreCase))) {
                            ret.Where(r => empId == r.Emplid && string.Equals(earningCode, r.EarnCode, StringComparison.OrdinalIgnoreCase)).ToList().ForEach(r => r.Hours += hours);
                        }
                        else {
                            var count = ret.Count(r => empId == r.Emplid);
                            ret.Add(new EarningReportModel {
                                EarnCode = earningCode,
                                Emplid = empId,
                                SeqNum = count+1,
                                Hours = hours
                            });
                        }
                    }
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error retrieving earning report. {ex.Message}");
                    ErrorMessage = "Failed to get earning report.";
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine(_earningReportHeader);
            foreach (var earn in ret) {
                sb.AppendLine($"{earn.RunId},{earn.Emplid},{earn.SeqNum},{earn.EarnCode},{earn.RateCode},{earn.Hours},{earn.Rate},{earn.Amount},{earn.FsDept},{earn.Combo},{earn.EmplRcd}");
            }
            return sb.ToString();
        }
    }
}
