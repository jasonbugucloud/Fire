using ApplicationInterface;
using ApplicationInterface.DataAccess;
using FireData.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;

namespace FireData.Repositories {
    public interface IOverTimeRepository {
        bool Request(OverTimeModel model, out int newId);
        bool Modify(int requestor, OverTimeModel model);
        bool Remove(int requestor, int id);
        bool Approve(int id, string approvedBy);
        List<OverTimeModel> Get(int firefighterId);
        List<OverTimeModel> GetRequestedList();
        int GetRequestedCount();
        bool GetNotification(int firefighterId);
        void SetNotified(int firefighterId);
        string ErrorMessage { get; }
    }

    public class OverTimeRepository : IOverTimeRepository {
        private readonly string _drConn = ConfigurationManager.ConnectionStrings["dr"]?.ConnectionString;
        public string ErrorMessage { get; private set; } = string.Empty;

        private OverTimeModel GetModel(int id) {
            var sql = $@"SELECT OVER_TIMES.ID,
                          OVER_TIMES.FIREFIGHTER_ID,
                          OVER_TIMES.START_AT,
                          OVER_TIMES.END_AT,
                          OVER_TIMES.REASON,
                          OVER_TIMES.REQUESTED,
                          OVER_TIMES.APPROVED,
                          OVER_TIMES.APPROVED_BY,
                          OVER_TIMES.EXPLANATION
                        FROM OVER_TIMES
                        WHERE OVER_TIMES.ID = {id}";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader();
                    if (reader.Read()) {
                        return new OverTimeModel {
                            Id = reader.GetSafeInt32(0),
                            FirefighterId = reader.GetSafeInt32(1),
                            StartAt = reader.GetSafeDateTime(2),
                            EndAt = reader.GetSafeDateTime(3),
                            Reason = string.Equals("OT", reader.GetSafeString(4), StringComparison.OrdinalIgnoreCase) ? OverTimeReason.Overtime : OverTimeReason.PartialActingPay,
                            Requested = reader.GetSafeDateTime(5),
                            Approved = reader.GetSafeDateTime(6),
                            ApprovedBy = reader.GetSafeString(7),
                            Explanation = reader.GetSafeString(8)
                        };
                    }
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error searching fire fighter overtime hours. {ex.Message}");
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
                    var sql = $"UPDATE OVER_TIMES SET APPROVED={OracleHelper.MakeSqlString(DateTime.Now)}, APPROVED_BY={OracleHelper.MakeSqlString(approvedBy)} WHERE ID={id}";
                    var cmd = new OracleCommand(sql, conn);
                    cmd.ExecuteNonQuery();

                    var cmd_pre = new OracleCommand($"SELECT COUNT(*) FROM WORKING_HOURS WHERE {OracleHelper.MakeSqlWhereString("FIREFIGHTER_ID", request.FirefighterId)} AND START_AT < {OracleHelper.MakeSqlString(request.StartAt)} AND END_AT > {OracleHelper.MakeSqlString(request.EndAt)}", conn);
                    var countOfExisting = Convert.ToInt32(cmd_pre.ExecuteScalar());
                    if (countOfExisting > 0) {
                        ErrorMessage = "Schedule conflicts with existing working hour records. Please inform fire fighter to verify it.";
                        tran.Rollback();
                        return false;
                    }
                    var dayCode = request.Reason == OverTimeReason.Overtime ? "OT" : "AOT";
                    var cmd_pre2 = new OracleCommand($"DELETE FROM WORKING_HOURS WHERE FIREFIGHTER_ID = {OracleHelper.MakeSqlString(request.FirefighterId)} AND TIMEBANK_ID = '{dayCode}{request.Id}'", conn);
                    cmd_pre2.ExecuteNonQuery();

                    var cmd2 = new OracleCommand($"INSERT INTO WORKING_HOURS (FIREFIGHTER_ID, DAY_CODE, START_AT, END_AT, CHECKED_IN, TIMEBANK_ID) VALUES ({OracleHelper.MakeSqlString(request.FirefighterId)}, '{dayCode}', {OracleHelper.MakeSqlString(request.StartAt)}, {OracleHelper.MakeSqlString(request.EndAt)}, 'Y', '{dayCode}{request.Id}')", conn);
                    cmd2.ExecuteNonQuery();
                    tran.Commit();

                    return true;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error approving fire fighter overtime hours. {ex.Message}");
                    tran.Rollback();
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }

            }
            return false;
        }

        public List<OverTimeModel> Get(int firefighterId) {
            var ret = new List<OverTimeModel>();
            var sql = $@"SELECT OVER_TIMES.ID,
                          OVER_TIMES.FIREFIGHTER_ID,
                          OVER_TIMES.START_AT,
                          OVER_TIMES.END_AT,
                          OVER_TIMES.REASON,
                          OVER_TIMES.REQUESTED,
                          OVER_TIMES.APPROVED,
                          OVER_TIMES.APPROVED_BY,
                          OVER_TIMES.EXPLANATION
                        FROM OVER_TIMES
                        WHERE OVER_TIMES.FIREFIGHTER_ID = {firefighterId}
                        ORDER BY OVER_TIMES.REQUESTED DESC";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader();
                    while (reader.Read()) {
                        ret.Add(new OverTimeModel {
                            Id = reader.GetSafeInt32(0),
                            FirefighterId = reader.GetSafeInt32(1),
                            StartAt = reader.GetSafeDateTime(2),
                            EndAt = reader.GetSafeDateTime(3),
                            Reason = string.Equals("OT", reader.GetSafeString(4), StringComparison.OrdinalIgnoreCase) ? OverTimeReason.Overtime : OverTimeReason.PartialActingPay,
                            Requested = reader.GetSafeDateTime(5),
                            Approved = reader.GetSafeDateTime(6),
                            ApprovedBy = reader.GetSafeString(7),
                            Explanation = reader.GetSafeString(8)
                        });
                    }
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error searching fire fighter overtime hours. {ex.Message}");
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
            return ret;
        }
        public int GetRequestedCount() {
            var sql = @"SELECT COUNT(*)
                        FROM OVER_TIMES
                        WHERE APPROVED   IS NULL";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error searching pending overtime requests. {ex.Message}");
                    return 0;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }
        public List<OverTimeModel> GetRequestedList() {
            var ret = new List<OverTimeModel>();
            var sql = @"SELECT OVER_TIMES.ID,
                            OVER_TIMES.FIREFIGHTER_ID,
                            OVER_TIMES.START_AT,
                            OVER_TIMES.END_AT,
                            OVER_TIMES.REASON,
                            OVER_TIMES.REQUESTED,
                            FIREFIGHTERS.FIRST_NAME,
                            FIREFIGHTERS.LAST_NAME,
                            OVER_TIMES.EXPLANATION
                        FROM OVER_TIMES
                        LEFT JOIN FIREFIGHTERS
                        ON OVER_TIMES.FIREFIGHTER_ID = FIREFIGHTERS.ID
                        WHERE OVER_TIMES.APPROVED   IS NULL
                        ORDER BY OVER_TIMES.REQUESTED DESC";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader();
                    while (reader.Read()) {
                        ret.Add(new OverTimeModel {
                            Id = reader.GetSafeInt32(0),
                            FirefighterId = reader.GetSafeInt32(1),
                            StartAt = reader.GetSafeDateTime(2),
                            EndAt = reader.GetSafeDateTime(3),
                            Reason = string.Equals("OT", reader.GetSafeString(4), StringComparison.OrdinalIgnoreCase) ? OverTimeReason.Overtime : OverTimeReason.PartialActingPay,
                            Requested = reader.GetSafeDateTime(5),
                            Name = reader.GetSafeString(6) + " " + reader.GetSafeString(7),
                            Explanation = reader.GetSafeString(8)
                        });
                    }
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error searching fire fighter overtime hours. {ex.Message}");
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
            return ret;
        }

        public bool Modify(int requestor, OverTimeModel model) {
            ErrorMessage = string.Empty;
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand($"SELECT FIREFIGHTER_ID FROM OVER_TIMES WHERE ID={model.Id} AND APPROVED IS NULL", conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader();
                    if (reader.Read()) {
                        var ffId = reader.GetSafeInt32(0);
                        if (requestor != ffId) {
                            ErrorMessage = "Invalid request.";
                            return false;
                        }
                        var cmd2 = new OracleCommand($"UPDATE OVER_TIMES SET START_AT = {OracleHelper.MakeSqlString(model.StartAt)}, END_AT = {OracleHelper.MakeSqlString(model.EndAt)}, REASON = '{(model.Reason == OverTimeReason.Overtime ? "OT" : "PA")}', EXPLANATION = {OracleHelper.MakeSqlString(model.Explanation)} WHERE ID={model.Id}", conn);
                        cmd2.ExecuteNonQuery();

                        return true;
                    }
                    else {
                        ErrorMessage = "Overtime request may not exist or be approved.";
                        return false;
                    }
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error updating fire fighter overtime hours. {ex.Message}");
                    ErrorMessage = "Failed to edit overtime request.";
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
                    var cmd = new OracleCommand($"SELECT FIREFIGHTER_ID FROM OVER_TIMES WHERE ID={id} AND APPROVED IS NULL", conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader();
                    if (reader.Read()) {
                        var ffId = reader.GetSafeInt32(0);
                        if (requestor != ffId) {
                            ErrorMessage = "Invalid request.";
                            return false;
                        }
                        var cmd2 = new OracleCommand($"DELETE FROM OVER_TIMES WHERE ID={id}", conn);
                        cmd2.ExecuteNonQuery();

                        return true;
                    }
                    else {
                        ErrorMessage = "Overtime request may not exist or be approved.";
                        return false;
                    }
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error removing fire fighter overtime hours. {ex.Message}");
                    ErrorMessage = "Failed to delete overtime request.";
                    return false;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }

        public bool Request(OverTimeModel model, out int newId) {
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
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand($"SELECT COUNT(*) FROM OVER_TIMES WHERE FIREFIGHTER_ID = {model.FirefighterId} AND START_AT < {OracleHelper.MakeSqlString(model.EndAt)} AND END_AT > {OracleHelper.MakeSqlString(model.StartAt)}", conn);
                    conn.Open();
                    var ret = Convert.ToInt32(cmd.ExecuteScalar());
                    if (ret != 0) {
                        ErrorMessage = "Overlayed with existing overtime records.";
                        return false;
                    }
                    var sql = $"INSERT INTO OVER_TIMES (FIREFIGHTER_ID, START_AT, END_AT, REASON, EXPLANATION) VALUES ({model.FirefighterId}, {OracleHelper.MakeSqlString(model.StartAt)}, {OracleHelper.MakeSqlString(model.EndAt)}, '{(model.Reason == OverTimeReason.Overtime ? "OT" : "PA")}', {OracleHelper.MakeSqlString(model.Explanation)})";
                    var cmd2 = new OracleCommand(sql, conn);
                    cmd2.ExecuteNonQuery();

                    var cmd3 = new OracleCommand("SELECT OVER_TIMES_SEQ.CURRVAL FROM DUAL", conn);
                    newId = Convert.ToInt32(cmd3.ExecuteScalar());

                    return true;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error adding fire fighter overtime hours. {ex.Message}");
                    ErrorMessage = "Failed to request overtime.";
                    return false;
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }
        public bool GetNotification(int firefighterId) {
            var sql = $@"SELECT COUNT(*)
                        FROM OVER_TIMES
                        WHERE FIREFIGHTER_ID = {firefighterId} AND APPROVED_NOTIFIED = 'N'";
            using (var conn = new OracleConnection(_drConn)) {
                try {
                    var cmd = new OracleCommand(sql, conn);
                    conn.Open();
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error searching fire fighter overtime approved notifications. {ex.Message}");
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
                    var cmd = new OracleCommand($"UPDATE OVER_TIMES SET APPROVED_NOTIFIED = 'Y' WHERE FIREFIGHTER_ID = {firefighterId}", conn);
                    conn.Open();

                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex) {
                    Logger.Instance.Error($"Error updating fire fighter overtime approved notifications. {ex.Message}");
                }
                finally {
                    if (conn.State != System.Data.ConnectionState.Closed)
                        conn.Close();
                }
            }
        }

    }
}
