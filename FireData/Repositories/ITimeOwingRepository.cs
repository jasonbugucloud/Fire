using ApplicationInterface;
using ApplicationInterface.DataAccess;
using FireData.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;

namespace FireData.Repositories {
    public interface ITimeOwingRepository {
        bool Request(TimeOwingModel model, out int newId);
        bool Modify(int requestor, TimeOwingModel model);
        bool Remove(int requestor, int id);
        bool Approve(int id, string approvedBy);
        List<TimeOwingModel> Get(int firefighterId);
        List<TimeOwingModel> GetRequestedList();
        int GetRequestedCount();
        bool GetNotification(int firefighterId);
        void SetNotified(int firefighterId);
        string ErrorMessage { get; }
    }

    public class TimeOwingRepository : ITimeOwingRepository {
        private readonly string _drConn = ConfigurationManager.ConnectionStrings["dr"]?.ConnectionString;
        public string ErrorMessage { get; private set; } = string.Empty;

        private TimeOwingModel GetModel(int id) {
            var sql = $@"SELECT TIME_OWINGS.ID,
                          TIME_OWINGS.FIREFIGHTER_ID,
                          TIME_OWINGS.START_AT,
                          TIME_OWINGS.END_AT,
                          TIME_OWINGS.TYPE,
                          TIME_OWINGS.REQUESTED,
                          TIME_OWINGS.APPROVED,
                          TIME_OWINGS.APPROVED_BY
                        FROM TIME_OWINGS
                        WHERE TIME_OWINGS.ID = {id}";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader();
                    if (reader.Read()) {
                        return new TimeOwingModel {
                            Id = reader.GetSafeInt32(0),
                            FirefighterId = reader.GetSafeInt32(1),
                            StartAt = reader.GetSafeDateTime(2),
                            EndAt = reader.GetSafeDateTime(3),
                            Type = string.Equals("TO", reader.GetSafeString(4), StringComparison.OrdinalIgnoreCase) ? TimeOwingType.TimeOwing : TimeOwingType.FamilyDay,
                            Requested = reader.GetSafeDateTime(5),
                            Approved = reader.GetSafeDateTime(6),
                            ApprovedBy = reader.GetSafeString(7)
                        };
                    }
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error searching fire fighter time owing hours. {ex.Message}");
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
            return null;
        }
        public bool Approve(int id, string approvedBy) {
            var request = GetModel(id);
            if (request == null) {
                ErrorMessage = "Request does not exist.";
                return false;
            }
            if (request.Approved != default(DateTime) && !string.IsNullOrEmpty(request.ApprovedBy)) {
                ErrorMessage = "Request has been already approved.";
                return false;
            }
            using (var conn = new OracleConnection(_drConn)) {
                conn.Open();
                var tran = conn.BeginTransaction();
                try {
                    var sql = $"UPDATE TIME_OWINGS SET APPROVED={OracleHelper.MakeSqlString(DateTime.Now)}, APPROVED_BY={OracleHelper.MakeSqlString(approvedBy)} WHERE ID={id}";
                    var cmd = new OracleCommand(sql, conn);
                    cmd.ExecuteNonQuery();

                    var cmd_pre = new OracleCommand($"SELECT COUNT(*) FROM WORKING_HOURS WHERE {OracleHelper.MakeSqlWhereString("FIREFIGHTER_ID", request.FirefighterId)} AND START_AT < {OracleHelper.MakeSqlString(request.StartAt)} AND END_AT > {OracleHelper.MakeSqlString(request.EndAt)}", conn);
                    var countOfExisting = Convert.ToInt32(cmd_pre.ExecuteScalar());
                    if (countOfExisting > 0) {
                        ErrorMessage = "Schedule conflicts with existing working hour records. Please inform fire fighter to verify it.";
                        tran.Rollback();
                        return false;
                    }

                    var dayCode = request.Type == TimeOwingType.FamilyDay ? "FD" : "TO";
                    var cmd_pre2 =new OracleCommand($"DELETE FROM WORKING_HOURS WHERE FIREFIGHTER_ID = {OracleHelper.MakeSqlString(request.FirefighterId)} AND TIMEBANK_ID = '{dayCode}{request.Id}'", conn);
                    cmd_pre2.ExecuteNonQuery();

                    var cmd2 = new OracleCommand($"INSERT INTO WORKING_HOURS (FIREFIGHTER_ID, DAY_CODE, START_AT, END_AT, CHECKED_IN, TIMEBANK_ID) VALUES ({OracleHelper.MakeSqlString(request.FirefighterId)}, '{dayCode}', {OracleHelper.MakeSqlString(request.StartAt)}, {OracleHelper.MakeSqlString(request.EndAt)}, 'Y', '{dayCode}{request.Id}')", conn);
                    cmd2.ExecuteNonQuery();

                    tran.Commit();
                    return true;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error approving fire fighter time owing hours. {ex.Message}");
                    tran.Rollback();
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }

            }
            return false;
        }
        public bool GetNotification(int firefighterId) {
            var sql = $@"SELECT COUNT(*)
                        FROM TIME_OWINGS
                        WHERE FIREFIGHTER_ID = {firefighterId} AND APPROVED_NOTIFIED = 'N'";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error searching fire fighter time owing approved notifications. {ex.Message}");
                    return false;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }
        public void SetNotified(int firefighterId) {
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand($"UPDATE TIME_OWINGS SET APPROVED_NOTIFIED = 'Y' WHERE FIREFIGHTER_ID = {firefighterId}", conn);
                    conn.Open();

                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error updating fire fighter time owing approved notifications. {ex.Message}");
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }
        public List<TimeOwingModel> Get(int firefighterId) {
            var ret = new List<TimeOwingModel>();
            var sql = $@"SELECT TIME_OWINGS.ID,
                          TIME_OWINGS.FIREFIGHTER_ID,
                          TIME_OWINGS.START_AT,
                          TIME_OWINGS.END_AT,
                          TIME_OWINGS.TYPE,
                          TIME_OWINGS.REQUESTED,
                          TIME_OWINGS.APPROVED,
                          TIME_OWINGS.APPROVED_BY
                        FROM TIME_OWINGS
                        WHERE TIME_OWINGS.FIREFIGHTER_ID = {firefighterId}
                        ORDER BY TIME_OWINGS.REQUESTED DESC";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader();
                    while (reader.Read()) {
                        ret.Add(new TimeOwingModel {
                            Id = reader.GetSafeInt32(0),
                            FirefighterId = reader.GetSafeInt32(1),
                            StartAt = reader.GetSafeDateTime(2),
                            EndAt = reader.GetSafeDateTime(3),
                            Type = string.Equals("TO", reader.GetSafeString(4), StringComparison.OrdinalIgnoreCase) ? TimeOwingType.TimeOwing : TimeOwingType.FamilyDay,
                            Requested = reader.GetSafeDateTime(5),
                            Approved = reader.GetSafeDateTime(6),
                            ApprovedBy = reader.GetSafeString(7)
                        });
                    }
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error searching fire fighter time owing hours. {ex.Message}");
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
            return ret;
        }

        public List<TimeOwingModel> GetRequestedList() {
            var ret = new List<TimeOwingModel>();
            var sql = @"SELECT TIME_OWINGS.ID,
                            TIME_OWINGS.FIREFIGHTER_ID,
                            TIME_OWINGS.START_AT,
                            TIME_OWINGS.END_AT,
                            TIME_OWINGS.TYPE,
                            TIME_OWINGS.REQUESTED,
                            FIREFIGHTERS.FIRST_NAME,
                            FIREFIGHTERS.LAST_NAME
                        FROM TIME_OWINGS
                        LEFT JOIN FIREFIGHTERS
                        ON TIME_OWINGS.FIREFIGHTER_ID = FIREFIGHTERS.ID
                        WHERE TIME_OWINGS.APPROVED   IS NULL
                        ORDER BY TIME_OWINGS.REQUESTED DESC";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader();
                    while (reader.Read()) {
                        ret.Add(new TimeOwingModel {
                            Id = reader.GetSafeInt32(0),
                            FirefighterId = reader.GetSafeInt32(1),
                            StartAt = reader.GetSafeDateTime(2),
                            EndAt = reader.GetSafeDateTime(3),
                            Type = string.Equals("TO", reader.GetSafeString(4), StringComparison.OrdinalIgnoreCase) ? TimeOwingType.TimeOwing : TimeOwingType.FamilyDay,
                            Requested = reader.GetSafeDateTime(5),
                            Name = reader.GetSafeString(6) + " " + reader.GetSafeString(7)
                        });
                    }
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error searching fire fighter time owing hours. {ex.Message}");
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
            return ret;
        }

        public bool Modify(int requestor, TimeOwingModel model) {
            ErrorMessage = string.Empty;
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand($"SELECT FIREFIGHTER_ID FROM TIME_OWINGS WHERE ID={model.Id} AND APPROVED IS NULL", conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader();
                    if (reader.Read()) {
                        var ffId = reader.GetSafeInt32(0);
                        if (requestor != ffId) {
                            ErrorMessage = "Invalid request.";
                            return false;
                        }
                        var cmd2 = new OracleCommand($"UPDATE TIME_OWINGS SET START_AT = {OracleHelper.MakeSqlString(model.StartAt)}, END_AT = {OracleHelper.MakeSqlString(model.EndAt)}, TYPE = '{(model.Type == TimeOwingType.FamilyDay ? "FD" : "TO")}' WHERE ID={model.Id}", conn);
                        cmd2.ExecuteNonQuery();

                        return true;
                    }
                    else {
                        ErrorMessage = "Time Owing request may not exist or be approved.";
                        return false;
                    }
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error updating fire fighter time owing hours. {ex.Message}");
                    ErrorMessage = "Failed to edit time owing request.";
                    return false;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }

        public bool Remove(int requestor, int id) {
            ErrorMessage = string.Empty;
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand($"SELECT FIREFIGHTER_ID FROM TIME_OWINGS WHERE ID={id} AND APPROVED IS NULL", conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader();
                    if (reader.Read()) {
                        var ffId = reader.GetSafeInt32(0);
                        if (requestor != ffId) {
                            ErrorMessage = "Invalid request.";
                            return false;
                        }
                        var cmd2 = new OracleCommand($"DELETE FROM TIME_OWINGS WHERE ID={id}", conn);
                        cmd2.ExecuteNonQuery();

                        return true;
                    }
                    else {
                        ErrorMessage = "Time Owing request may not exist or be approved.";
                        return false;
                    }
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error removing fire fighter time owing hours. {ex.Message}");
                    ErrorMessage = "Failed to delete time owing request.";
                    return false;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }

        public bool Request(TimeOwingModel model, out int newId) {
            ErrorMessage = string.Empty;
            newId = -1;
            if (model == null) {
                ErrorMessage = "Wrong Data";
                return false;
            }
            if (model.EndAt < model.StartAt) {
                var temp = model.StartAt;
                model.StartAt = model.EndAt;
                model.EndAt = temp;
            }
            if (model.StartAt < DateTime.Now || model.EndAt < DateTime.Now) {
                ErrorMessage = "You are only able to request time owing for future.";
                return false;
            }
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand($"SELECT COUNT(*) FROM TIME_OWINGS WHERE FIREFIGHTER_ID = {model.FirefighterId} AND START_AT < {OracleHelper.MakeSqlString(model.EndAt)} AND END_AT > {OracleHelper.MakeSqlString(model.StartAt)}", conn);
                    conn.Open();
                    var ret = Convert.ToInt32(cmd.ExecuteScalar());
                    if (ret != 0) {
                        ErrorMessage = "Overlayed with existing time owing records.";
                        return false;
                    }
                    var sql = $"INSERT INTO TIME_OWINGS (FIREFIGHTER_ID, START_AT, END_AT, TYPE) VALUES ({model.FirefighterId}, {OracleHelper.MakeSqlString(model.StartAt)}, {OracleHelper.MakeSqlString(model.EndAt)}, '{(model.Type == TimeOwingType.FamilyDay ? "FD" : "TO")}')";
                    var cmd2 = new OracleCommand(sql, conn);
                    cmd2.ExecuteNonQuery();

                    var cmd3 = new OracleCommand("SELECT TIME_OWINGS_SEQ.CURRVAL FROM DUAL", conn);
                    newId = Convert.ToInt32(cmd3.ExecuteScalar());

                    return true;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error adding fire fighter time owing hours. {ex.Message}");
                    ErrorMessage = "Failed to request time owing.";
                    return false;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }

        public int GetRequestedCount() {
            var sql = @"SELECT COUNT(*)
                        FROM TIME_OWINGS
                        WHERE APPROVED   IS NULL";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error searching pending time owing requests. {ex.Message}");
                    return 0;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }
    }
}
