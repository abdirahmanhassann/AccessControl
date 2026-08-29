using AccessControl.Database;
using System.Data;
using System.Reflection;
using static AccessControl.Helpers.ApiHelper;
using static AccessControl.Model.ApiResponses;

namespace AccessControl.Processor
{
    public partial class AccessProcessor
    {
        public GenericResponse GetDepartments(int userId) =>
            TryTable(userId, () => _db.GetDepartments());

        public GenericResponse InsertDepartment(int userId, int siteId, string name, string? description)
        {
            try
            {
                int id = _db.InsertDepartment(siteId, name, description);
                return CreateSuccessResponseWithData(id);
            }
            catch (Exception ex)
            {
                _db.LogError(MethodBase.GetCurrentMethod()?.Module + "/" + MethodBase.GetCurrentMethod()?.Name!, ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse UpdateDepartment(int userId, int id, int siteId, string name, string? description, bool isActive)
        {
            try
            {
                _db.UpdateDepartment(id, siteId, name, description, isActive);
                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(MethodBase.GetCurrentMethod()?.Module + "/" + MethodBase.GetCurrentMethod()?.Name!, ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse GetDepartmentRoomMappings(int userId) =>
            TryTable(userId, () => _db.GetDepartmentRoomMappings());

        public GenericResponse InsertDepartmentRoomMapping(int userId, int departmentId, int roomId)
        {
            try
            {
                int id = _db.InsertDepartmentRoomMapping(departmentId, roomId);
                return CreateSuccessResponseWithData(id);
            }
            catch (Exception ex)
            {
                _db.LogError(MethodBase.GetCurrentMethod()?.Module + "/" + MethodBase.GetCurrentMethod()?.Name!, ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse DeleteDepartmentRoomMapping(int userId, int id)
        {
            try
            {
                _db.DeleteDepartmentRoomMapping(id);
                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(MethodBase.GetCurrentMethod()?.Module + "/" + MethodBase.GetCurrentMethod()?.Name!, ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse GetDepartmentWorkerMappings(int userId) =>
            TryTable(userId, () => _db.GetDepartmentWorkerMappings());

        public GenericResponse InsertDepartmentWorkerMapping(int userId, int departmentId, int workerId)
        {
            try
            {
                int id = _db.InsertDepartmentWorkerMapping(departmentId, workerId);
                return CreateSuccessResponseWithData(id);
            }
            catch (Exception ex)
            {
                _db.LogError(MethodBase.GetCurrentMethod()?.Module + "/" + MethodBase.GetCurrentMethod()?.Name!, ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse DeleteDepartmentWorkerMapping(int userId, int id)
        {
            try
            {
                _db.DeleteDepartmentWorkerMapping(id);
                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(MethodBase.GetCurrentMethod()?.Module + "/" + MethodBase.GetCurrentMethod()?.Name!, ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse GetDepartmentManagerMappings(int userId) =>
            TryTable(userId, () => _db.GetDepartmentManagerMappings());

        public GenericResponse InsertDepartmentManagerMapping(int userId, int departmentId, int managerUserId, bool isPrimary)
        {
            try
            {
                int id = _db.InsertDepartmentManagerMapping(departmentId, managerUserId, isPrimary);
                return CreateSuccessResponseWithData(id);
            }
            catch (Exception ex)
            {
                _db.LogError(MethodBase.GetCurrentMethod()?.Module + "/" + MethodBase.GetCurrentMethod()?.Name!, ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse UpdateDepartmentManagerMapping(int userId, int id, int departmentId, int managerUserId, bool isPrimary)
        {
            try
            {
                _db.UpdateDepartmentManagerMapping(id, departmentId, managerUserId, isPrimary);
                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(MethodBase.GetCurrentMethod()?.Module + "/" + MethodBase.GetCurrentMethod()?.Name!, ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse DeleteDepartmentManagerMapping(int userId, int id)
        {
            try
            {
                _db.DeleteDepartmentManagerMapping(id);
                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(MethodBase.GetCurrentMethod()?.Module + "/" + MethodBase.GetCurrentMethod()?.Name!, ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse GetWorkerSiteMappings(int userId) =>
            TryTable(userId, () => _db.GetWorkerSiteMappings());

        public GenericResponse InsertWorkerSiteMapping(int userId, int workerId, int siteId)
        {
            try
            {
                int id = _db.InsertWorkerSiteMapping(workerId, siteId);
                return CreateSuccessResponseWithData(id);
            }
            catch (Exception ex)
            {
                _db.LogError(MethodBase.GetCurrentMethod()?.Module + "/" + MethodBase.GetCurrentMethod()?.Name!, ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse DeleteWorkerSiteMapping(int userId, int id)
        {
            try
            {
                _db.DeleteWorkerSiteMapping(id);
                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(MethodBase.GetCurrentMethod()?.Module + "/" + MethodBase.GetCurrentMethod()?.Name!, ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse GetManagersForRoom(int roomId)
        {
            try
            {
                if (roomId < 1) return CreateFailureResponse("Room id is required.");
                return CreateSuccessResponseWithData(_db.GetManagersForRoom(roomId));
            }
            catch (Exception ex)
            {
                _db.LogError(MethodBase.GetCurrentMethod()?.Module + "/" + MethodBase.GetCurrentMethod()?.Name!, ex.Message + " " + ex.StackTrace, 0);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        private GenericResponse TryTable(int userId, Func<DataTable> load)
        {
            try
            {
                return CreateSuccessResponseWithData(load());
            }
            catch (Exception ex)
            {
                _db.LogError(MethodBase.GetCurrentMethod()?.Module + "/" + MethodBase.GetCurrentMethod()?.Name!, ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }
    }
}
