using Microsoft.Data.SqlClient;
using System.Data;

namespace AccessControl.Database
{
    public partial class DatabaseLogic
    {
        public DataTable GetDepartments(int? id = null, int? siteId = null)
        {
            List<SqlParameter> p = new();
            if (id.HasValue) p.Add(_conn.GenerateInputInteger("@Id", id.Value));
            if (siteId.HasValue) p.Add(_conn.GenerateInputInteger("@SiteId", siteId.Value));
            return _conn.ExecuteStoredProcedureAsDataTable("DepartmentsGet", p.ToArray());
        }

        public int InsertDepartment(int siteId, string name, string? description)
        {
            List<SqlParameter> p = new()
            {
                _conn.GenerateInputInteger("@SiteId", siteId),
                _conn.GenerateInputString("@Name", name, 200),
                _conn.GenerateInputString("@Description", description, 500)
            };
            return _conn.ExecuteStoredProcedureAsScalarInt("DepartmentsInsert", p.ToArray());
        }

        public void UpdateDepartment(int id, int siteId, string name, string? description, bool isActive)
        {
            List<SqlParameter> p = new()
            {
                _conn.GenerateInputInteger("@Id", id),
                _conn.GenerateInputInteger("@SiteId", siteId),
                _conn.GenerateInputString("@Name", name, 200),
                _conn.GenerateInputString("@Description", description, 500),
                _conn.GenerateInputBool("@IsActive", isActive)
            };
            _conn.ExecuteStoredProcedureNoReturn("DepartmentsUpdate", p.ToArray());
        }

        public DataTable GetDepartmentRoomMappings(int? id = null, int? departmentId = null, int? roomId = null)
        {
            List<SqlParameter> p = new();
            if (id.HasValue) p.Add(_conn.GenerateInputInteger("@Id", id.Value));
            if (departmentId.HasValue) p.Add(_conn.GenerateInputInteger("@DepartmentId", departmentId.Value));
            if (roomId.HasValue) p.Add(_conn.GenerateInputInteger("@RoomId", roomId.Value));
            return _conn.ExecuteStoredProcedureAsDataTable("DepartmentRoomMappingGet", p.ToArray());
        }

        public int InsertDepartmentRoomMapping(int departmentId, int roomId)
        {
            List<SqlParameter> p = new()
            {
                _conn.GenerateInputInteger("@DepartmentId", departmentId),
                _conn.GenerateInputInteger("@RoomId", roomId)
            };
            return _conn.ExecuteStoredProcedureAsScalarInt("DepartmentRoomMappingInsert", p.ToArray());
        }

        public void DeleteDepartmentRoomMapping(int id)
        {
            _conn.ExecuteStoredProcedureNoReturn(
                "DepartmentRoomMappingDelete",
                new[] { _conn.GenerateInputInteger("@Id", id) });
        }

        public DataTable GetDepartmentWorkerMappings(int? id = null, int? departmentId = null, int? workerId = null)
        {
            List<SqlParameter> p = new();
            if (id.HasValue) p.Add(_conn.GenerateInputInteger("@Id", id.Value));
            if (departmentId.HasValue) p.Add(_conn.GenerateInputInteger("@DepartmentId", departmentId.Value));
            if (workerId.HasValue) p.Add(_conn.GenerateInputInteger("@WorkerId", workerId.Value));
            return _conn.ExecuteStoredProcedureAsDataTable("DepartmentWorkerMappingGet", p.ToArray());
        }

        public int InsertDepartmentWorkerMapping(int departmentId, int workerId)
        {
            List<SqlParameter> p = new()
            {
                _conn.GenerateInputInteger("@DepartmentId", departmentId),
                _conn.GenerateInputInteger("@WorkerId", workerId)
            };
            return _conn.ExecuteStoredProcedureAsScalarInt("DepartmentWorkerMappingInsert", p.ToArray());
        }

        public void DeleteDepartmentWorkerMapping(int id)
        {
            _conn.ExecuteStoredProcedureNoReturn(
                "DepartmentWorkerMappingDelete",
                new[] { _conn.GenerateInputInteger("@Id", id) });
        }

        public DataTable GetDepartmentManagerMappings(int? id = null, int? departmentId = null, int? userId = null)
        {
            List<SqlParameter> p = new();
            if (id.HasValue) p.Add(_conn.GenerateInputInteger("@Id", id.Value));
            if (departmentId.HasValue) p.Add(_conn.GenerateInputInteger("@DepartmentId", departmentId.Value));
            if (userId.HasValue) p.Add(_conn.GenerateInputInteger("@UserId", userId.Value));
            return _conn.ExecuteStoredProcedureAsDataTable("DepartmentManagerMappingGet", p.ToArray());
        }

        public int InsertDepartmentManagerMapping(int departmentId, int userId, bool isPrimary)
        {
            List<SqlParameter> p = new()
            {
                _conn.GenerateInputInteger("@DepartmentId", departmentId),
                _conn.GenerateInputInteger("@UserId", userId),
                _conn.GenerateInputBool("@IsPrimary", isPrimary)
            };
            return _conn.ExecuteStoredProcedureAsScalarInt("DepartmentManagerMappingInsert", p.ToArray());
        }

        public void UpdateDepartmentManagerMapping(int id, int departmentId, int userId, bool isPrimary)
        {
            List<SqlParameter> p = new()
            {
                _conn.GenerateInputInteger("@Id", id),
                _conn.GenerateInputInteger("@DepartmentId", departmentId),
                _conn.GenerateInputInteger("@UserId", userId),
                _conn.GenerateInputBool("@IsPrimary", isPrimary)
            };
            _conn.ExecuteStoredProcedureNoReturn("DepartmentManagerMappingUpdate", p.ToArray());
        }

        public void DeleteDepartmentManagerMapping(int id)
        {
            _conn.ExecuteStoredProcedureNoReturn(
                "DepartmentManagerMappingDelete",
                new[] { _conn.GenerateInputInteger("@Id", id) });
        }

        public DataTable GetWorkerSiteMappings(int? id = null, int? workerId = null, int? siteId = null)
        {
            List<SqlParameter> p = new();
            if (id.HasValue) p.Add(_conn.GenerateInputInteger("@Id", id.Value));
            if (workerId.HasValue) p.Add(_conn.GenerateInputInteger("@WorkerId", workerId.Value));
            if (siteId.HasValue) p.Add(_conn.GenerateInputInteger("@SiteId", siteId.Value));
            return _conn.ExecuteStoredProcedureAsDataTable("WorkerSiteMappingGet", p.ToArray());
        }

        public int InsertWorkerSiteMapping(int workerId, int siteId)
        {
            List<SqlParameter> p = new()
            {
                _conn.GenerateInputInteger("@WorkerId", workerId),
                _conn.GenerateInputInteger("@SiteId", siteId)
            };
            return _conn.ExecuteStoredProcedureAsScalarInt("WorkerSiteMappingInsert", p.ToArray());
        }

        public void DeleteWorkerSiteMapping(int id)
        {
            _conn.ExecuteStoredProcedureNoReturn(
                "WorkerSiteMappingDelete",
                new[] { _conn.GenerateInputInteger("@Id", id) });
        }

        public DataTable GetManagersForRoom(int roomId)
        {
            return _conn.ExecuteStoredProcedureAsDataTable(
                "ManagersForRoomGet",
                new[] { _conn.GenerateInputInteger("@RoomId", roomId) });
        }

        public bool WorkerCanAccessRoom(int workerId, int roomId)
        {
            return _conn.ExecuteStoredProcedureAsScalarInt(
                "WorkerCanAccessRoom",
                new[]
                {
                    _conn.GenerateInputInteger("@WorkerId", workerId),
                    _conn.GenerateInputInteger("@RoomId", roomId)
                }) == 1;
        }

        public bool ManagerCanApprove(int approverUserId, int? roomId, int? accessRequestId)
        {
            List<SqlParameter> p = new()
            {
                _conn.GenerateInputInteger("@ApproverUserId", approverUserId)
            };
            if (roomId.HasValue) p.Add(_conn.GenerateInputInteger("@RoomId", roomId.Value));
            if (accessRequestId.HasValue) p.Add(_conn.GenerateInputInteger("@AccessRequestId", accessRequestId.Value));
            return _conn.ExecuteStoredProcedureAsScalarInt("ManagerCanApprove", p.ToArray()) == 1;
        }
    }
}
