using ApplicationInterface;
using ApplicationInterface.DataAccess;
using ApplicationInterface.Models;
using FireWorkforceManagement.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;

namespace FireWorkforceManagement.Repositories {
    public interface IDailyRosterRepository {
        List<DailyRosterSchedule> GetAvailableSchedules(DateTime from, DateTime to, string apparatus);
        List<DailyRoster> GetDailyRoster(string apparatus, DateTime from, DateTime to);
        bool SaveDailyRoster(List<DailyRoster> drs);
        List<DailyRosterDashboard> GetDailyRosterDashboardList(string date);
        bool Checkin(string user, string password, int workhourId);
        DailyAttendance GetDailyAttendanceReport(DateTime date);
        List<DailyRoster> GetDailyRosterByDateTimeRange(DateTime from, DateTime to);
        IEnumerable<Dictionary<string, string>> GetYearlyRosterData(int year);
        string ErrorMessage { get; }
    }

    public class DailyRosterRepository : IDailyRosterRepository {
        private readonly IConfigRepository _config;
        private readonly IFireFightersRepository _firefighter;
        private readonly string _drConn = ConfigurationManager.ConnectionStrings["dr"]?.ConnectionString;
        private string _errorMessage;

        public DailyRosterRepository(IConfigRepository repo, IFireFightersRepository repo2) {
            _config = repo;
            _firefighter = repo2;
        }

        public string ErrorMessage => _errorMessage;

        public List<DailyRosterSchedule> GetAvailableSchedules(DateTime from, DateTime to, string apparatus) {
            _errorMessage = string.Empty;
            var sql = $"SELECT W.ID, W.FIREFIGHTER_ID, F.LAST_NAME, F.FIRST_NAME, W.DAY_CODE, W.START_AT, W.END_AT, S.SKILLS, F.APPARATUS FROM WORKING_HOURS W LEFT OUTER JOIN FIREFIGHTERS F ON W.FIREFIGHTER_ID = F.ID LEFT OUTER JOIN (SELECT FIREFIGHTER_ID, LISTAGG(SKILL, ',') WITHIN GROUP(ORDER BY SKILL) AS SKILLS FROM SKILLS GROUP BY FIREFIGHTER_ID) S ON F.ID = S.FIREFIGHTER_ID WHERE F.ACTIVE = 'Y' AND W.ALLOCATED = 'N' AND W.START_AT < {OracleHelper.MakeSqlString(to)} AND W.END_AT > {OracleHelper.MakeSqlString(from)}";

            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                    var ret = new List<DailyRosterSchedule>();
                    while (reader.Read()) {
                        var dayCode = reader.GetSafeString(4);
                        //firefighter must be working
                        if (!_config.WorkingDayCodeList.Contains(dayCode)) {
                            continue;
                        }
                        ret.Add(new DailyRosterSchedule {
                            WorkHourId = reader.GetSafeInt32(0),
                            FirefighterId = reader.GetSafeInt32(1),
                            LastName = reader.GetSafeString(2),
                            FirstName = reader.GetSafeString(3),
                            DayCode = dayCode,
                            StartsAt = reader.GetSafeDateTime(5),
                            EndsAt = reader.GetSafeDateTime(6),
                            Skills = $"[{reader.GetSafeString(7)}]",
                            Apparatus = reader.GetSafeString(8)
                        });
                    }
                    return ret.OrderByDescending(r => r.Apparatus == apparatus).ToList();
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error retrieving available schedule records. {ex.Message}");
                    _errorMessage = "Failed to get available schedules.";
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }

            return new List<DailyRosterSchedule>();
        }

        public List<DailyRoster> GetDailyRoster(string apparatus, DateTime from, DateTime to) {
            _errorMessage = string.Empty;
            var sql = $"SELECT D.POSITION, W.ID, W.FIREFIGHTER_ID, F.LAST_NAME, F.FIRST_NAME, W.DAY_CODE, W.START_AT, W.END_AT, S.SKILLS FROM DAILY_ROSTER D LEFT OUTER JOIN WORKING_HOURS W ON D.WORKHOUR_ID = W.ID LEFT OUTER JOIN FIREFIGHTERS F ON W.FIREFIGHTER_ID = F.ID LEFT OUTER JOIN (SELECT FIREFIGHTER_ID, LISTAGG(SKILL, ',') WITHIN GROUP(ORDER BY SKILL) AS SKILLS FROM SKILLS GROUP BY FIREFIGHTER_ID) S ON F.ID = S.FIREFIGHTER_ID WHERE D.APPARATUS = {OracleHelper.MakeSqlString(apparatus)} AND D.START_AT = {OracleHelper.MakeSqlString(from)} AND D.END_AT = {OracleHelper.MakeSqlString(to)}";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                    var ret = new List<DailyRoster>();
                    var positionList = _config.GetPositionList(apparatus);
                    foreach (var position in positionList) {
                        ret.Add(new DailyRoster {
                            Apparatus = apparatus,
                            Position = position.Item1,
                            From = from,
                            To = to,
                            Skill = position.Item2,
                            Allocations = new List<DailyRosterSchedule>()
                        });
                    }
                    while (reader.Read()) {
                        var position = reader.GetSafeString(0);
                        if (ret.Any(d => d.Position == position)) {
                            var dr = ret.First(d => d.Position == position);
                            dr.Allocations.Add(
                                new DailyRosterSchedule {
                                    WorkHourId = reader.GetSafeInt32(1),
                                    FirefighterId = reader.GetSafeInt32(2),
                                    LastName = reader.GetSafeString(3),
                                    FirstName = reader.GetSafeString(4),
                                    DayCode = reader.GetSafeString(5),
                                    StartsAt = reader.GetSafeDateTime(6),
                                    EndsAt = reader.GetSafeDateTime(7),
                                    Skills = $"[{reader.GetSafeString(8)}]"
                                });
                        }
                    }
                    return ret;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error retrieving allocated schedule records. {ex.Message}");
                    _errorMessage = "Failed to get allocated schedules.";
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
            return new List<DailyRoster>();
        }
        public bool SaveDailyRoster(List<DailyRoster> drs) {
            _errorMessage = string.Empty;
            if (drs != null && drs.Any()) {
                var sql = new List<string>();
                var apparatus = drs.First().Apparatus;
                var from = drs.First().From;
                var to = drs.First().To;

                var origins = GetDailyRoster(apparatus, from, to);

                foreach (var origin in origins) {
                    var position = origin.Position;
                    //compare origin with new dr by position, to get remove list and add list
                    var dr = drs.First(d => d.Position == position);
                    if (dr != null) {
                        var removeList = origin.Allocations.Where(d => dr.Allocations.All(d2 => d2.WorkHourId != d.WorkHourId));
                        foreach (var r in removeList) {
                            sql.Add($"DELETE FROM DAILY_ROSTER WHERE {OracleHelper.MakeSqlWhereString("WORKHOUR_ID", r.WorkHourId)} AND APPARATUS={OracleHelper.MakeSqlString(apparatus)} AND POSITION={OracleHelper.MakeSqlString(position)} AND START_AT={OracleHelper.MakeSqlString(from)} AND END_AT={OracleHelper.MakeSqlString(to)}");
                            sql.Add($"UPDATE WORKING_HOURS SET ALLOCATED='N' WHERE {OracleHelper.MakeSqlWhereString("ID", r.WorkHourId)}");
                        }
                        var addList = dr.Allocations.Where(d => origin.Allocations.All(d2 => d2.WorkHourId != d.WorkHourId)).OrderBy(d => d.StartsAt);
                        var leftFrom = from;
                        foreach (var a in addList) {
                            if (leftFrom == to) {
                                break;
                            }
                            sql.Add($"INSERT INTO DAILY_ROSTER (WORKHOUR_ID, APPARATUS, POSITION, START_AT, END_AT) VALUES({OracleHelper.MakeSqlString(a.WorkHourId)}, {OracleHelper.MakeSqlString(apparatus)}, {OracleHelper.MakeSqlString(position)}, {OracleHelper.MakeSqlString(from)}, {OracleHelper.MakeSqlString(to)})");
                            if (a.StartsAt < leftFrom) {
                                if (a.EndsAt <= to) {
                                    //a.StartsAt ~ leftFrom is not used; leftFrom ~ a.EndsAt is used
                                    sql.Add($"INSERT INTO WORKING_HOURS (FIREFIGHTER_ID, DAY_CODE, START_AT, END_AT) VALUES({OracleHelper.MakeSqlString(a.FirefighterId)}, {OracleHelper.MakeSqlString(a.DayCode)}, {OracleHelper.MakeSqlString(a.StartsAt)}, {OracleHelper.MakeSqlString(leftFrom)})");
                                    sql.Add($"UPDATE WORKING_HOURS SET ALLOCATED='Y', START_AT={OracleHelper.MakeSqlString(leftFrom)} WHERE {OracleHelper.MakeSqlWhereString("ID", a.WorkHourId)}");
                                    leftFrom = a.EndsAt;
                                }
                                else {
                                    //a.StartsAt ~ leftFrom is not used; leftFrom ~ to is used; to ~ a.EndsAt is not used
                                    sql.Add($"INSERT INTO WORKING_HOURS (FIREFIGHTER_ID, DAY_CODE, START_AT, END_AT) VALUES({OracleHelper.MakeSqlString(a.FirefighterId)}, {OracleHelper.MakeSqlString(a.DayCode)}, {OracleHelper.MakeSqlString(a.StartsAt)}, {OracleHelper.MakeSqlString(leftFrom)})");
                                    sql.Add($"UPDATE WORKING_HOURS SET ALLOCATED='Y', START_AT={OracleHelper.MakeSqlString(leftFrom)}, END_AT={OracleHelper.MakeSqlString(to)} WHERE {OracleHelper.MakeSqlWhereString("ID", a.WorkHourId)}");
                                    sql.Add($"INSERT INTO WORKING_HOURS (FIREFIGHTER_ID, DAY_CODE, START_AT, END_AT) VALUES({OracleHelper.MakeSqlString(a.FirefighterId)}, {OracleHelper.MakeSqlString(a.DayCode)}, {OracleHelper.MakeSqlString(to)}, {OracleHelper.MakeSqlString(a.EndsAt)})");
                                    leftFrom = to;
                                }
                            }
                            else if (a.StartsAt >= leftFrom) {
                                if (a.EndsAt <= to) {
                                    //a.StartsAt ~ a.EndsAt is used
                                    sql.Add($"UPDATE WORKING_HOURS SET ALLOCATED='Y' WHERE {OracleHelper.MakeSqlWhereString("ID", a.WorkHourId)}");
                                    leftFrom = a.EndsAt;
                                }
                                else {
                                    //a.StartsAt ~ to is used; to ~ a.EndsAt is not used
                                    sql.Add($"UPDATE WORKING_HOURS SET ALLOCATED='Y', END_AT={OracleHelper.MakeSqlString(to)} WHERE {OracleHelper.MakeSqlWhereString("ID", a.WorkHourId)}");
                                    sql.Add($"INSERT INTO WORKING_HOURS (FIREFIGHTER_ID, DAY_CODE, START_AT, END_AT) VALUES({OracleHelper.MakeSqlString(a.FirefighterId)}, {OracleHelper.MakeSqlString(a.DayCode)}, {OracleHelper.MakeSqlString(to)}, {OracleHelper.MakeSqlString(a.EndsAt)})");
                                    leftFrom = to;
                                }
                            }
                        }
                    }
                }
                if (sql.Any()) {
                    using (var conn = new OracleConnection(_drConn)) {
                        conn.Open();
                        var trans = conn.BeginTransaction();
                        try {
                            foreach (var s in sql) {
                                var cmd = new OracleCommand(s, conn);
                                cmd.ExecuteNonQuery();
                            }
                            trans.Commit();
                            return true;
                        }
                        catch (Exception ex) {
                            Logger.Instance.Error($"Error saving daily roster. [Apparatus: {apparatus}][From: {from: MM/dd/yyyy HH:mm}][To: {to: MM/dd/yyyy HH:mm}]{ex.Message}");
                            _errorMessage = "Failed to save daily roster.";
                            trans.Rollback();
                            return false;
                        }
                        finally {
                            if (conn.State != System.Data.ConnectionState.Closed)
                                conn.Close();
                        }
                    }
                }
                _errorMessage = "No change.";
                return true;
            }
            _errorMessage = "Invalid request.";
            return false;
        }

        public List<DailyRoster> GetDailyRosterByDateTimeRange(DateTime from, DateTime to) {
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand($"SELECT D.APPARATUS, D.POSITION, W.ID, W.FIREFIGHTER_ID, F.LAST_NAME, F.FIRST_NAME, W.DAY_CODE, W.START_AT, W.END_AT, W.CHECKED_IN, F.USER_NAME, F.PLATOON FROM DAILY_ROSTER D LEFT OUTER JOIN WORKING_HOURS W ON D.WORKHOUR_ID = W.ID LEFT OUTER JOIN FIREFIGHTERS F ON W.FIREFIGHTER_ID = F.ID WHERE D.START_AT={OracleHelper.MakeSqlString(from)} AND D.END_AT={OracleHelper.MakeSqlString(to)} ORDER BY W.START_AT", conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                    var ret = new List<DailyRoster>();
                    while (reader.Read()) {
                        var apparatus = reader.GetSafeString(0);
                        var position = reader.GetSafeString(1);
                        if (ret.Any(dr => dr.Apparatus == apparatus && dr.Position == position)) {
                            foreach (var dr in ret) {
                                if (dr.Apparatus == apparatus && dr.Position == position) {
                                    dr.Allocations.Add(new DailyRosterSchedule {
                                        WorkHourId = reader.GetSafeInt32(2),
                                        FirefighterId = reader.GetSafeInt32(3),
                                        LastName = reader.GetSafeString(4),
                                        FirstName = reader.GetSafeString(5),
                                        DayCode = reader.GetSafeString(6),
                                        StartsAt = reader.GetSafeDateTime(7),
                                        EndsAt = reader.GetSafeDateTime(8),
                                        CheckedIn = reader.GetSafeString(9) == "Y",
                                        UserName = reader.GetSafeString(10),
                                        Platoon = reader.GetSafeString(11)
                                    });
                                }
                            }
                        }
                        else {
                            ret.Add(new DailyRoster {
                                Apparatus = apparatus,
                                Position = position,
                                From = from,
                                To = to,
                                Allocations = new List<DailyRosterSchedule> {
                                    new DailyRosterSchedule {
                                        WorkHourId = reader.GetSafeInt32(2),
                                        FirefighterId = reader.GetSafeInt32(3),
                                        LastName = reader.GetSafeString(4),
                                        FirstName = reader.GetSafeString(5),
                                        DayCode = reader.GetSafeString(6),
                                        StartsAt = reader.GetSafeDateTime(7),
                                        EndsAt = reader.GetSafeDateTime(8),
                                        CheckedIn = reader.GetSafeString(9) == "Y",
                                        UserName = reader.GetSafeString(10),
                                        Platoon = reader.GetSafeString(11)
                                    }
                                }
                            });
                        }
                    }
                    return ret;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error retrieving daily roster records. {ex.Message}");
                    _errorMessage = "Failed to get daily roster for today.";
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
            return new List<DailyRoster>();
        }

        /// <summary>
        /// Retrieve daily attendance list data for daily attendance report
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public DailyAttendance GetDailyAttendanceReport(DateTime date) {
            var ret = new DailyAttendance {
                Date = date,
                AttendanceList = new List<Tuple<string, List<DailyAttendanceLocationData>>>(),
                OffDutyList = new List<WorkHour>()
            };

            var from = new DateTime(date.Year, date.Month, date.Day, 7, 0, 0);
            var to = from.AddDays(1);
            var rosterList = GetDailyRosterByDateTimeRange(from, to);

            var apparatusList = _config.ApparatusList;
            var platoons = new List<string>();
            var dailyRoster = new List<DailyRosterDashboard>();
            foreach (var apparatus in apparatusList) {
                var positionList = _config.GetPositionList(apparatus.Value);
                if (positionList.Any()) {
                    var drdb = new DailyRosterDashboard {
                        Apparatus = apparatus,
                        RosterList = positionList.Select(p => new Roster { Position = p.Item1 }).ToList()
                    };
                    foreach (var roster in drdb.RosterList) {
                        var workHours = rosterList.Where(cr => cr.Position == roster.Position && cr.Apparatus == apparatus.Value).ToList();
                        if (workHours.Any()) {
                            roster.Allocations = workHours.First().Allocations;
                            platoons.AddRange(roster.Allocations.Select(a => a.Platoon));
                        }
                    }
                    dailyRoster.Add(drdb);
                }
            }

            //if most of ff are from one Platoon, the report is for the Platoon. Any ff from another Platoon is replacement.
            var thePlatoon = _config.PlatoonList.Select(p => new Tuple<string, string, int>(p.Text, p.Value, platoons.Count(pla => string.Equals(pla, p.Value, StringComparison.OrdinalIgnoreCase)))).OrderByDescending(t => t.Item3).First();
            ret.PlatoonText = thePlatoon.Item1;
            ret.Platoon = thePlatoon.Item2;
            //get count of on-duty ffs in platoon
            ret.OnDutyStaff = (from a in rosterList from b in a.Allocations where b.Platoon == thePlatoon.Item2 select b.FullName).Distinct().Count();
            foreach (var dr in dailyRoster) {
                var attendance = new Tuple<string, List<DailyAttendanceLocationData>>(dr.Apparatus.Text, new List<DailyAttendanceLocationData>());
                foreach (var r in dr.RosterList) {
                    var dald = new DailyAttendanceLocationData {
                        Position = r.Position
                    };
                    foreach (var a in r.Allocations/*.Where(al => al.CheckedIn.HasValue && al.CheckedIn.Value)*/.OrderBy(al => al.StartsAt)) {
                        if (string.IsNullOrWhiteSpace(dald.Name) && string.Equals(a.Platoon, thePlatoon.Item2, StringComparison.OrdinalIgnoreCase)) {
                            dald.Name = a.FullName;
                            dald.DayCode = a.DayCode;
                            dald.Time = $"{a.StartsAt.ToString("HH:mm", CultureInfo.InvariantCulture)} - {a.EndsAt.ToString("HH:mm", CultureInfo.InvariantCulture)}";
                            continue;
                        }
                        dald.Replacement = a.FullName;
                        dald.Platoon = a.Platoon;
                        dald.ReplaceDayCode = a.DayCode;
                        dald.ReplaceTime = $"{a.StartsAt.ToString("HH:mm", CultureInfo.InvariantCulture)} - {a.EndsAt.ToString("HH:mm", CultureInfo.InvariantCulture)}";
                    }
                    attendance.Item2.Add(dald);
                }
                ret.AttendanceList.Add(attendance);
            }

            return ret;
        }
        private List<DailyRoster> GetDailyRosterByDate(DateTime date) {
            var today = date.Date;
            var yesterday = today.AddDays(-1);
            var tomorrow = today.AddDays(1);
            var from = date.Hour > 7 ? new DateTime(today.Year, today.Month, today.Day, 7, 0, 0) : new DateTime(yesterday.Year, yesterday.Month, yesterday.Day, 7, 0, 0);
            var to = date.Hour > 7 ? new DateTime(tomorrow.Year, tomorrow.Month, tomorrow.Day, 7, 0, 0) : new DateTime(today.Year, today.Month, today.Day, 7, 0, 0);

            return GetDailyRosterByDateTimeRange(from, to);
        }

        public List<DailyRosterDashboard> GetDailyRosterDashboardList(string date) {
            var ret = new List<DailyRosterDashboard>();
            DateTime theDate;
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out theDate)) {
                return ret;
            }
            var currentRosters = GetDailyRosterByDate(theDate);
            var apparatusList = _config.ApparatusList;
            foreach (var apparatus in apparatusList) {
                var positionList = _config.GetPositionList(apparatus.Value);
                if (positionList.Any()) {
                    var drdb = new DailyRosterDashboard {
                        Apparatus = apparatus,
                        RosterList = positionList.Select(p => new Roster { Position = p.Item1 }).ToList()
                    };
                    foreach (var roster in drdb.RosterList) {
                        var workHours = currentRosters.Where(cr => cr.Position == roster.Position && cr.Apparatus == apparatus.Value).ToList();
                        if (workHours.Any()) {
                            roster.Allocations = workHours.First().Allocations;
                        }
                    }
                    ret.Add(drdb);
                }
            }

            return ret;
        }

        public bool Checkin(string user, string password, int workhourId) {
            var userProfile = new UserProfile {
                UserName = user,
                HashPassword = password
            };
            if (userProfile.IsAuthenticated) {
                using (var conn = new OracleConnection(_drConn)) {
                    try {
                        var cmd = new OracleCommand($"UPDATE WORKING_HOURS SET CHECKED_IN='Y' WHERE ID={workhourId}", conn);
                        conn.Open();
                        cmd.ExecuteNonQuery();

                        _errorMessage = "Checked in successfully";
                        return true;
                    }
                    catch (Exception ex) {
                        Logger.Instance.Error($"Error checking in. {ex.Message}");
                        _errorMessage = "Failed to check in.";
                        return false;
                    }
                    finally {
                        if (conn.State != System.Data.ConnectionState.Closed)
                            conn.Close();
                    }
                }
            }
            _errorMessage = "Incorrect password.";
            return false;
        }

        public IEnumerable<Dictionary<string, string>> GetYearlyRosterData(int year)
        {
            var ret = new List<Dictionary<string, string>>();
            var from = new DateTime(year, 1, 1, 7, 0, 0);
            var to = new DateTime(year + 1, 1, 1, 7, 0, 0);

            var apparatuses = _config.ApparatusList.Where(l => l.Value.Contains("Pump") || l.Value.Contains("Aerial"))
                .ToList();
            var positions = _config.GetPositionList(apparatuses.First().Value);
            var firefighters = _firefighter.GetFireFighterList(null);
            var sql =
                $"SELECT D.APPARATUS, D.POSITION, W.FIREFIGHTER_ID, (W.END_AT - W.START_AT) AS DAYS FROM WORKING_HOURS W LEFT JOIN DAILY_ROSTER D ON W.ID = D.WORKHOUR_ID WHERE W.CHECKED_IN='Y' AND W.START_AT >= {OracleHelper.MakeSqlString(from)} AND W.END_AT <= {OracleHelper.MakeSqlString(to)}";

            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                    var rosterDataList = new List<RosterData>();
                    while (reader.Read()) {
                        rosterDataList.Add(new RosterData
                        {
                            FirefighterId = reader.GetSafeInt32(2),
                            Apparatus = reader.GetSafeString(0),
                            Position = reader.GetSafeString(1),
                            Days = reader.GetSafeDouble(3)
                        });
                    }
                    var firefighterIdList = (from r in rosterDataList
                        group r by r.FirefighterId
                        into grp
                        select grp.First().FirefighterId).ToList();
                    foreach (var ff in firefighterIdList)
                    {
                        var retItem = CreateDictionary(apparatuses, positions);
                        retItem["Name"] = firefighters.FirstOrDefault(f =>
                            string.Equals(f.Value, ff.ToString(), StringComparison.Ordinal))?.Text;
                        foreach (var apparatus in apparatuses)
                        {
                            var days =
                            (from r in rosterDataList
                                where r.FirefighterId == ff && r.Apparatus == apparatus.Value
                                select r.Days).Sum();
                            retItem[apparatus.Value] = Math.Abs(days) < 0.01 ? string.Empty : days.ToString("F2");
                        }
                        foreach (var pos in positions)
                        {
                            var days = (from r in rosterDataList
                                where r.FirefighterId == ff && !string.IsNullOrEmpty(r.Position) && r.Position.IndexOf(pos.Item2, StringComparison.OrdinalIgnoreCase) > -1
                                select r.Days).Sum();
                            retItem[pos.Item2] = Math.Abs(days) < 0.01 ? string.Empty : days.ToString("F2");
                        }
                        ret.Add(retItem);
                    }
                    return ret;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error retrieving allocated schedule records. {ex.Message}");
                    _errorMessage = "Failed to get allocated schedules.";
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }

            return ret;
        }

        private Dictionary<string, string> CreateDictionary(List<SelectListItem> apparatuses,
            List<Tuple<string, string>> positions)
        {
            var retItem = new Dictionary<string, string> {{"Name", string.Empty}};
            foreach (var apparatus in apparatuses) {
                retItem.Add(apparatus.Value, string.Empty);
            }
            foreach (var pos in positions) {
                if (!retItem.ContainsKey(pos.Item2))
                    retItem.Add(pos.Item2, string.Empty);
            }
            return retItem;
        }
        public class RosterData
        {
            public int FirefighterId { get; set; }
            public string Apparatus { get; set; }
            public string Position { get; set; }
            public double Days { get; set; }
        }
    }
}
