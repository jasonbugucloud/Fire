using ApplicationInterface;
using ApplicationInterface.DataAccess;
using FireWorkforceManagement.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Mvc;
using FireData.Models;

namespace FireWorkforceManagement.Repositories {
    public interface IFireFightersRepository {
        List<FireFighter> Search(FireFighterSearch search);
        FireFighter Get(int id);
        FireFighter Get(string username);
        bool Deactivate(int id);
        bool Retire(int id);
        bool UpdateSkills(int id, List<string> skills);
        List<SelectListItem> GetFireFighterList(int? id, bool activeOnly = false);
        bool ActiveFirefighterExists(int id);
        OrganizationChartModel GetCommitteeChart();
        int GetIdByUsername(string userName);
        string ErrorMessage { get; }
    }

    public class FireFightersRepository : IFireFightersRepository {
        private IConfigRepository _config;
        private readonly string _drConn = ConfigurationManager.ConnectionStrings["dr"]?.ConnectionString;
        private Dictionary<string, string> _mappings = OracleHelper.GetColumnMappings<FireFighter>();
        public FireFightersRepository(IConfigRepository repo) {
            _config = repo;
        }
        public string ErrorMessage { get; private set; } = string.Empty;

        string IFireFightersRepository.ErrorMessage => throw new NotImplementedException();

        public List<FireFighter> Search(FireFighterSearch search) {
            ErrorMessage = string.Empty;
            var ret = new List<FireFighter>();
            //validate search criteria first
            var where = new List<string> {
                "ROWNUM > -1"
            };
            //ActiveOnly is boolean, if it's true, add it into query condition;
            if (search.ActiveOnly) {
                where.Add(OracleHelper.MakeSqlWhereString("ACTIVE", "Y"));
            }
            else {
                where.Add("ACTIVE <> 'R'");//don't show retired firefighters
            }
            //Apparatus must be * or one of config list, if it's not *, add it into query condition;
            if (search.Apparatus != "*") {
                if (_config.ApparatusList.Any(l => l.Value == search.Apparatus)) {
                    where.Add(OracleHelper.MakeSqlWhereString("APPARATUS", search.Apparatus));
                }
                else {
                    ErrorMessage = "Invalid Apparatus search criteria!";
                    return ret;
                }
            }
            //Platoon must be * or one of config list, if it's not *, add it into query condition;
            if (search.Platoon != "*") {
                if (_config.PlatoonList.Any(p => p.Value == search.Platoon)) {
                    where.Add(OracleHelper.MakeSqlWhereString("PLATOON", search.Platoon));
                }
                else {
                    ErrorMessage = "Invalid Platoon search criteria!";
                    return ret;
                }
            }
            //Rank must be * or one of config list, if it's not *, add it into query condition;
            if (search.Rank != "*") {
                if (_config.RankList.Any(p => p.Value == search.Rank)) {
                    where.Add(OracleHelper.MakeSqlWhereString("RANK", search.Rank));
                }
                else {
                    ErrorMessage = "Invalid Rank search criteria!";
                    return ret;
                }
            }
            //LastName can be any value, if !string.isNullOrWhitespace(LastName), add it into query condition.
            if (!string.IsNullOrWhiteSpace(search.LastName)) {
                where.Add(OracleHelper.MakeSqlWhereString("UPPER(LAST_NAME)", $"%{search.LastName.ToUpperInvariant()}%"));
            }

            //construct search query and run it against DB, output list of FireFighter if success.
            var sql = $"SELECT ID, LAST_NAME, FIRST_NAME, PHONE, CELL_PHONE, PLATOON, START_DATE, RANK, ACTIVE, APPARATUS, NOTE, STATION FROM FIREFIGHTERS WHERE {string.Join(" AND ", where)} ORDER BY START_DATE, LAST_NAME";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader();
                    var ids = new List<string>();
                    while (reader.Read()) {
                        ret.Add(new FireFighter {
                            Id = reader.GetSafeInt32(0),
                            LastName = reader.GetSafeString(1),
                            FirstName = reader.GetSafeString(2),
                            PhoneNumber = reader.GetSafeString(3),
                            CellNumber = reader.GetSafeString(4),
                            Platoon = reader.GetSafeString(5),
                            PlatoonText = GetPlatoonText(reader.GetSafeString(5)),
                            StartDate = reader.GetSafeDateTime(6),
                            Rank = reader.GetSafeString(7),
                            RankText = GetRankText(reader.GetSafeString(7)),
                            Active = reader.GetSafeString(8) == "Y",
                            Apparatus = reader.GetSafeString(9),
                            ApparatusText = GetApparatusText(reader.GetSafeString(9)),
                            Notes = reader.GetSafeString(10),
                            Station = reader.GetSafeString(11)
                        });
                        ids.Add(reader.GetSafeInt32(0).ToString());
                    }
                    if (ids.Any()) {
                        var cmd2 = new OracleCommand($"SELECT FIREFIGHTER_ID, SKILL FROM SKILLS WHERE FIREFIGHTER_ID IN ({string.Join(", ", ids)})", conn);
                        var reader2 = cmd2.ExecuteReader();
                        while (reader2.Read()) {
                            foreach (var ff in ret) {
                                if (ff.Id == reader2.GetSafeInt32(0)) {
                                    ff.Skills.Add(reader2.GetSafeString(1));
                                    break;
                                }
                            }
                        }
                    }
                    return ret;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error searching fire fighter records. {ex.Message}");
                    ErrorMessage = "Failed to search fire fighters.";
                    return ret;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }

        public FireFighter Get(int id) {
            ErrorMessage = string.Empty;
            var sql = $"SELECT ID, LAST_NAME, FIRST_NAME, PHONE, CELL_PHONE, PLATOON, START_DATE, RANK, ACTIVE, APPARATUS, NOTE, USER_NAME, STATION FROM FIREFIGHTERS WHERE {OracleHelper.MakeSqlWhereString("ID", id)} AND ACTIVE <> 'R'";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                    if (reader.Read()) {
                        var ret = new FireFighter {
                            Id = reader.GetSafeInt32(0),
                            LastName = reader.GetSafeString(1),
                            FirstName = reader.GetSafeString(2),
                            PhoneNumber = reader.GetSafeString(3),
                            CellNumber = reader.GetSafeString(4),
                            Platoon = reader.GetSafeString(5),
                            StartDate = reader.GetSafeDateTime(6),
                            Rank = reader.GetSafeString(7),
                            Active = reader.GetSafeString(8) == "Y",
                            Apparatus = reader.GetSafeString(9),
                            Notes = reader.GetSafeString(10),
                            UserName = reader.GetSafeString(11),
                            Station = reader.GetSafeString(12)
                        };
                        var cmd2 = new OracleCommand($"SELECT SKILL FROM SKILLS WHERE {OracleHelper.MakeSqlWhereString("FIREFIGHTER_ID", ret.Id)}", conn);
                        var reader2 = cmd2.ExecuteReader();
                        while (reader2.Read()) {
                            ret.Skills.Add(reader2.GetSafeString(0));
                        }
                        return ret;
                    }
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error retrieving fire fighter records by {id}. {ex.Message}");
                    ErrorMessage = "Failed to get fire fighter.";
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
            return new FireFighter();
        }

        private string GetPlatoonText (string value) {
            var platoon = _config.PlatoonList.FirstOrDefault(p => p.Value == value);
            return platoon?.Text ?? value;
        }
        private string GetRankText(string value) {
            var rank = _config.RankList.FirstOrDefault(p => p.Value == value);
            return rank?.Text ?? value;
        }
        private string GetApparatusText(string value) {
            var apparatus = _config.ApparatusList.FirstOrDefault(p => p.Value == value);
            return apparatus?.Text ?? value;
        }

        public bool Deactivate(int id) {
            ErrorMessage = string.Empty;

            using (var conn = new OracleConnection(_drConn)) {
                conn.Open();
                try {
                    var sql = $"UPDATE FIREFIGHTERS SET ACTIVE = 'N' WHERE ID = {id}";
                    var cmd = new OracleCommand(sql, conn);
                    cmd.ExecuteNonQuery();

                    return true;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error saving fire fighter record. {ex.Message}");
                    ErrorMessage = "Failed to save fire fighter record.";
                    return false;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }
        public bool Retire(int id) {
            ErrorMessage = string.Empty;

            using (var conn = new OracleConnection(_drConn)) {
                conn.Open();
                try {
                    var sql = $"UPDATE FIREFIGHTERS SET ACTIVE = 'R', RETIRED = {OracleHelper.MakeSqlString(DateTime.Now)} WHERE ID = {id}";
                    var cmd = new OracleCommand(sql, conn);
                    cmd.ExecuteNonQuery();

                    return true;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error saving fire fighter record. {ex.Message}");
                    ErrorMessage = "Failed to save fire fighter record.";
                    return false;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }
        public bool UpdateSkills(int id, List<string> skills) {
            ErrorMessage = string.Empty;

            using (var conn = new OracleConnection(_drConn)) {
                conn.Open();
                var trans = conn.BeginTransaction();
                try {
                    var cmd2 = new OracleCommand($"DELETE FROM SKILLS WHERE FIREFIGHTER_ID = {id}", conn);
                    cmd2.ExecuteNonQuery();

                    foreach (var s in skills) {
                        var cmd3 = new OracleCommand($"INSERT INTO SKILLS (FIREFIGHTER_ID, SKILL) VALUES ({id}, {OracleHelper.MakeSqlString(s)})", conn);
                        cmd3.ExecuteNonQuery();
                    }
                    trans.Commit();
                    return true;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error saving fire fighter skill records. {ex.Message}");
                    trans.Rollback();
                    ErrorMessage = "Failed to save fire fighter skill records.";
                    return false;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }

        public List<SelectListItem> GetFireFighterList(int? id, bool activeOnly = false) {
            ErrorMessage = string.Empty;
            var sql = $"SELECT ID, LAST_NAME, FIRST_NAME FROM FIREFIGHTERS";
            var where = new List<string>();
            if (activeOnly) {
                where.Add("ACTIVE = 'Y'");
            }
            else {
                where.Add("ACTIVE <> 'R'");
            }
            if (id.HasValue) {
                if (id.Value < 0 && _config.PlatoonList.Any(p => p.Value == Math.Abs(id.Value).ToString())) {
                    where.Add($"PLATOON = '{Math.Abs(id.Value)}'");
                }
                else {
                    where.Add($"ID = {id.Value}");
                }
            }
            if (where.Any()) {
                sql += " WHERE " + string.Join(" AND ", where);
            }
            sql += " ORDER BY FIRST_NAME";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                    var ret = new List<SelectListItem>();
                    while (reader.Read()) {
                        ret.Add(new SelectListItem {
                            Text = $"{reader.GetSafeString(2)} {reader.GetSafeString(1)}",
                            Value = reader.GetSafeInt32(0).ToString()
                        });
                    }
                    return ret;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error retrieving active fire fighter list. {ex.Message}");
                    ErrorMessage = "Failed to get active fire fighter list.";
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
            return new List<SelectListItem>();
        }

        public bool ActiveFirefighterExists(int id) {
            var sql = $"SELECT COUNT(ID) FROM FIREFIGHTERS WHERE ACTIVE = 'Y' AND ID = {id}";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var count = Convert.ToInt32(cmd.ExecuteScalar());
                    return count == 1;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error check if active fire fighter exists. {ex.Message}");
                    ErrorMessage = "Failed to check if active fire fighter exists.";
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
            return false;
        }

        public OrganizationChartModel GetCommitteeChart() {
            ErrorMessage = string.Empty;
            var ret = new OrganizationChartModel
            {
                CommitteeList = new List<string>(),
                MembershipList = new List<CommitteeMembershipModel>()
            };
            var sql = @"SELECT c.COMMITTEE, c.POSITION, f.First_NAME, f.LAST_NAME FROM COMMITTEES c LEFT JOIN COMMITTEE_FIREFIGHTER cf ON c.COMMITTEE = cf.COMMITTEE AND cf.POSITION = c.POSITION LEFT JOIN FIREFIGHTERS f ON f.ID = cf.FIREFIGHTER_ID";
            using (var conn = new OracleConnection(_drConn)) {
                conn.Open();
                try {
                    var cmd = new OracleCommand(sql, conn);
                    var reader = cmd.ExecuteReader();

                    while (reader.Read()) {
                        var committee = reader.GetSafeString(0);
                        var position = reader.GetSafeString(1);
                        var firstName = reader.GetSafeString(2);
                        var lastName = reader.GetSafeString(3);

                        if (ret.MembershipList.Any(c =>
                            string.Equals(c.Committee, committee, StringComparison.Ordinal) &&
                            string.Equals(c.Position, position, StringComparison.Ordinal))) {
                            if (!(string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))) {
                                ret.MembershipList.First(c =>
                                        string.Equals(c.Committee, committee, StringComparison.Ordinal) &&
                                        string.Equals(c.Position, position, StringComparison.Ordinal)).FireFighters
                                    .Add($"{firstName} {lastName}");
                            }
                        }
                        else {
                            var member = new CommitteeMembershipModel {
                                Committee = committee,
                                Position = position,
                                FireFighters = new List<string>()
                            };
                            if (!(string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))) {
                                member.FireFighters.Add($"{firstName} {lastName}");
                            }
                            ret.MembershipList.Add(member);
                        }
                        if (!ret.CommitteeList.Any(c => string.Equals(c, committee, StringComparison.Ordinal)))
                        {
                            ret.CommitteeList.Add(committee);
                        }
                    }

                    return ret;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error retrieving committee chart data. {ex.Message}");
                    ErrorMessage = "Failed to retrieve committee chart data.";
                    return ret;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }

        }

        public int GetIdByUsername(string userName) {
            var ff = Get(userName);
            return ff == null ? -1 : ff.Id;
        }

        public FireFighter Get(string userName) {
            ErrorMessage = string.Empty;
            var sql = $"SELECT ID, RANK FROM FIREFIGHTERS WHERE {OracleHelper.MakeSqlWhereString("USER_NAME", userName)}";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                    if (reader.Read()) {
                        return new FireFighter{
                            Id = reader.GetSafeInt32(0),
                            Rank = reader.GetSafeString(1)
                        };
                    }
                    return null;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error retrieving fire fighter records by {userName}. {ex.Message}");
                    ErrorMessage = "Failed to get fire fighter.";
                    return null;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }
    }

}
