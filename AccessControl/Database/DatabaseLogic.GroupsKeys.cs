using Microsoft.Data.SqlClient;
using System.Data;

namespace AccessControl.Database
{
    // Multi-room access requests and keys (Database/MultiRoomKeys.sql).
    public partial class DatabaseLogic
    {
        /// <summary>
        /// Saves a group, one access request + pending approval per room, and the keys asked for,
        /// in one transaction. Returns one row per room: GroupId, AccessRequestId, RoomId.
        /// </summary>
        public DataTable InsertAccessRequestGroup(
            int workerId,
            int approverUserId,
            string? reason,
            string? workType,
            string? description,
            string? supervisorName,
            DateTime? workFrom,
            DateTime? workTo,
            string itemsJson)
        {
            List<SqlParameter> sqlParams = new()
            {
                _conn.GenerateInputInteger("@WorkerId", workerId),
                _conn.GenerateInputInteger("@ApproverUserId", approverUserId),
                _conn.GenerateInputString("@Reason", reason, 500),
                _conn.GenerateInputString("@WorkType", workType, 200),
                _conn.GenerateInputString("@Description", description, 1000),
                _conn.GenerateInputString("@SupervisorName", supervisorName, 200),
                _conn.GenerateInputString("@Items", itemsJson, -1),
            };
            if (workFrom.HasValue) sqlParams.Add(_conn.GenerateInputDateTime("@WorkFrom", workFrom.Value));
            if (workTo.HasValue) sqlParams.Add(_conn.GenerateInputDateTime("@WorkTo", workTo.Value));
            return _conn.ExecuteStoredProcedureAsDataTable("AccessRequestGroupsInsert", sqlParams.ToArray());
        }

        /// <summary>Every room of a group, with its latest approval.</summary>
        public DataTable GetAccessRequestGroup(int groupId)
        {
            return _conn.ExecuteStoredProcedureAsDataTable(
                "AccessRequestGroupGet",
                new[] { _conn.GenerateInputInteger("@GroupId", groupId) });
        }

        /// <summary>The group an access request belongs to, or 0 for a single-room request.</summary>
        public int GetAccessRequestGroupId(int accessRequestId)
        {
            if (accessRequestId < 1) return 0;
            DataTable dt = GetAccessRequests(accessRequestId, 0, 0, null, null, 0, 1);
            if (dt.Rows.Count == 0 || !dt.Columns.Contains("GroupId") || dt.Rows[0]["GroupId"] == DBNull.Value)
            {
                return 0;
            }
            return Convert.ToInt32(dt.Rows[0]["GroupId"]);
        }

        /// <returns>True for exactly one caller, once every room of the group is decided.</returns>
        public bool ClaimGroupNotification(int groupId)
        {
            return _conn.ExecuteStoredProcedureAsScalarInt(
                "AccessRequestGroupsClaimNotification",
                new[] { _conn.GenerateInputInteger("@GroupId", groupId) }) > 0;
        }

        /// <summary>Keys out past their return-by time, each claimed once for a reminder.</summary>
        public List<int> ClaimLateKeys(DateTime now)
        {
            DataTable dt = _conn.ExecuteStoredProcedureAsDataTable(
                "AccessRequestKeysClaimLate",
                new[] { _conn.GenerateInputDateTime("@Now", now) });
            return dt.Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r["Id"])).ToList();
        }

        public DataTable GetKeys(
            int id = 0,
            int roomId = 0,
            int workAreaId = 0,
            int siteId = 0,
            bool includeInactive = false,
            DateTime? workFrom = null,
            DateTime? workTo = null)
        {
            List<SqlParameter> sqlParams = new();
            if (id > 0) sqlParams.Add(_conn.GenerateInputInteger("@Id", id));
            if (roomId > 0) sqlParams.Add(_conn.GenerateInputInteger("@RoomId", roomId));
            if (workAreaId > 0) sqlParams.Add(_conn.GenerateInputInteger("@WorkAreaId", workAreaId));
            if (siteId > 0) sqlParams.Add(_conn.GenerateInputInteger("@SiteId", siteId));
            sqlParams.Add(_conn.GenerateInputBool("@IncludeInactive", includeInactive));
            if (workFrom.HasValue) sqlParams.Add(_conn.GenerateInputDateTime("@WorkFrom", workFrom.Value));
            if (workTo.HasValue) sqlParams.Add(_conn.GenerateInputDateTime("@WorkTo", workTo.Value));
            return _conn.ExecuteStoredProcedureAsDataTable("KeysGet", sqlParams.ToArray());
        }

        public int InsertKey(int roomId, string label, string? tagNumber, string? notes)
        {
            return _conn.ExecuteStoredProcedureAsScalarInt(
                "KeysInsert",
                new[]
                {
                    _conn.GenerateInputInteger("@RoomId", roomId),
                    _conn.GenerateInputString("@Label", label, 100),
                    _conn.GenerateInputString("@TagNumber", tagNumber, 50),
                    _conn.GenerateInputString("@Notes", notes, 500),
                });
        }

        public bool UpdateKey(int id, string label, string? tagNumber, string? notes, bool isActive)
        {
            return _conn.ExecuteStoredProcedureAsScalarInt(
                "KeysUpdate",
                new[]
                {
                    _conn.GenerateInputInteger("@Id", id),
                    _conn.GenerateInputString("@Label", label, 100),
                    _conn.GenerateInputString("@TagNumber", tagNumber, 50),
                    _conn.GenerateInputString("@Notes", notes, 500),
                    _conn.GenerateInputBool("@IsActive", isActive),
                }) > 0;
        }

        public DataTable GetAccessRequestKeys(
            int id = 0,
            int accessRequestId = 0,
            int groupId = 0,
            string? status = null,
            bool outstanding = false)
        {
            List<SqlParameter> sqlParams = new();
            if (id > 0) sqlParams.Add(_conn.GenerateInputInteger("@Id", id));
            if (accessRequestId > 0) sqlParams.Add(_conn.GenerateInputInteger("@AccessRequestId", accessRequestId));
            if (groupId > 0) sqlParams.Add(_conn.GenerateInputInteger("@GroupId", groupId));
            if (!string.IsNullOrWhiteSpace(status)) sqlParams.Add(_conn.GenerateInputString("@Status", status, 20));
            sqlParams.Add(_conn.GenerateInputBool("@Outstanding", outstanding));
            return _conn.ExecuteStoredProcedureAsDataTable("AccessRequestKeysGet", sqlParams.ToArray());
        }

        /// <summary>Requested keys on this room follow the room's decision.</summary>
        public void DecideAccessRequestKeys(int accessRequestId, bool approved)
        {
            _conn.ExecuteStoredProcedureAsScalarInt(
                "AccessRequestKeysDecide",
                new[]
                {
                    _conn.GenerateInputInteger("@AccessRequestId", accessRequestId),
                    _conn.GenerateInputBool("@Approved", approved),
                });
        }

        /// <returns>True if the key was in a state that allows the move.</returns>
        public bool UpdateAccessRequestKey(int id, string status, int userId, string? note)
        {
            return _conn.ExecuteStoredProcedureAsScalarInt(
                "AccessRequestKeysUpdate",
                new[]
                {
                    _conn.GenerateInputInteger("@Id", id),
                    _conn.GenerateInputString("@Status", status, 20),
                    _conn.GenerateInputInteger("@UserId", userId),
                    _conn.GenerateInputString("@Note", note, 500),
                }) > 0;
        }
    }
}
