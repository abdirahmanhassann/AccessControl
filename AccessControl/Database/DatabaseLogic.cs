using Microsoft.Data.SqlClient;
using System.Data;
using Twilio.Rest.Voice.V1;
using static AccessControl.Model.Model;

namespace AccessControl.Database
{
    public class DatabaseLogic
    {
        private DatabaseConnectivity _conn;

        public DatabaseLogic(string connectionString, bool keepAlive)
        {
            _conn = new(connectionString, keepAlive);
        }
        public void Signup(string email, string firstName, string lastName, string password, string salt, string phoneNumber)
        {
            List<SqlParameter> sqlParams = new();
            sqlParams.Add(_conn.GenerateInputString("@Email", email, 255));
            sqlParams.Add(_conn.GenerateInputString("@FirstName", firstName, 100));
            sqlParams.Add(_conn.GenerateInputString("@lastName", lastName, 100));
            sqlParams.Add(_conn.GenerateInputString("@Password", password, 50));
            sqlParams.Add(_conn.GenerateInputString("@Salt", salt, 50));
            sqlParams.Add(_conn.GenerateInputString("@PhoneNumber", phoneNumber));
            _conn.ExecuteStoredProcedureNoReturn("CreateUser", sqlParams.ToArray());
        }
        public DataRow GetAccountByEmail(string email)
        {
            List<SqlParameter> sqlParams = new();
            sqlParams.Add(_conn.GenerateInputString("@Email", email, 255));
            return _conn.ExecuteStoredProcedureAsDataRow("GetUserByEmail", sqlParams.ToArray());
        }

        public void LogError(string source, string message, int userId)
        {
            List<SqlParameter> sqlParams = new();
            sqlParams.Add(_conn.GenerateInputString("@SourceLocation", source, 512));
            sqlParams.Add(_conn.GenerateInputString("@ErrorMessage", message, 4000));
            sqlParams.Add(_conn.GenerateInputint("@AccountId", userId));
            _conn.ExecuteStoredProcedureNoReturn("LogError", sqlParams.ToArray());
        }
        public int TokenValid(string token)
        {
            List<SqlParameter> sqlParams = new();
            sqlParams.Add(_conn.GenerateInputString("@Token", token, 50));
            return _conn.ExecuteStoredProcedureAsScalarInt("CheckToken", sqlParams.ToArray());
        }

        public void AddAccessToken(int accountId, string token)
        {
            List<SqlParameter> sqlParams = new();
            sqlParams.Add(_conn.GenerateInputint("@AccountId", accountId));
            sqlParams.Add(_conn.GenerateInputString("@Token", token, 50));
            _conn.ExecuteStoredProcedureNoReturn("AddAccessToken", sqlParams.ToArray());
        }

        public void ExtendAccessToken(string token)
        {
            List<SqlParameter> sqlParams = new();
            sqlParams.Add(_conn.GenerateInputString("@Token", token, 50));
            _conn.ExecuteStoredProcedureNoReturn("ExtendAccessToken", sqlParams.ToArray());
        }


        public DataTable GetSites()
        {
            List<SqlParameter> sqlParams = new();

            return _conn.ExecuteStoredProcedureAsDataTable(
                "SitesGet",
                sqlParams.ToArray());
        }

        public void InsertSite(
            string name,
            string? address,
            string? reference,
            string? qrCodeIdentifier)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputString("@Name", name, 200));

            sqlParams.Add(
                _conn.GenerateInputString("@Address", address, 500));

            sqlParams.Add(
                _conn.GenerateInputString("@Reference", reference, 100));

            sqlParams.Add(
                _conn.GenerateInputString("@QrCodeIdentifier", qrCodeIdentifier, 100));

            _conn.ExecuteStoredProcedureNoReturn(
                "SitesInsert",
                sqlParams.ToArray());
        }

        public void UpdateSite(
            int id,
            string name,
            string? address,
            string? reference,
            string? qrCodeIdentifier,
            bool isActive)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id));

            sqlParams.Add(
                _conn.GenerateInputString("@Name", name, 200));

            sqlParams.Add(
                _conn.GenerateInputString("@Address", address, 500));

            sqlParams.Add(
                _conn.GenerateInputString("@Reference", reference, 100));

            sqlParams.Add(
                _conn.GenerateInputString("@QrCodeIdentifier", qrCodeIdentifier, 100));

            sqlParams.Add(
                _conn.GenerateInputBool("@IsActive", isActive));

            _conn.ExecuteStoredProcedureNoReturn(
                "SitesUpdate",
                sqlParams.ToArray());
        }

        public DataRow GetSiteByQrCode(string qrCodeIdentifier)
        {
            List<SqlParameter> sqlParams = new();
            sqlParams.Add(
                _conn.GenerateInputString("@QrCodeIdentifier", qrCodeIdentifier, 100));
            return _conn.ExecuteStoredProcedureAsDataRow(
                "SitesGetByQrCode",
                sqlParams.ToArray());
        }


        // ============================================================
        // WORK AREAS
        // ============================================================

        public DataTable GetWorkAreas(int siteId = 0)
        {
            List<SqlParameter> sqlParams = new();
            if (siteId > 0)
            {
                sqlParams.Add(_conn.GenerateInputInteger("@SiteId", siteId));
            }

            return _conn.ExecuteStoredProcedureAsDataTable(
                "WorkAreasGet",
                sqlParams.ToArray());
        }

        public void InsertWorkArea(
            int siteId,
            string name,
            string? description)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@SiteId", siteId));

            sqlParams.Add(
                _conn.GenerateInputString("@Name", name, 200));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Description",
                    description,
                    500));

            _conn.ExecuteStoredProcedureNoReturn(
                "WorkAreasInsert",
                sqlParams.ToArray());
        }

        public void UpdateWorkArea(
            int id,
            int siteId,
            string name,
            string? description,
            bool isActive)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id));

            sqlParams.Add(
                _conn.GenerateInputInteger("@SiteId", siteId));

            sqlParams.Add(
                _conn.GenerateInputString("@Name", name, 200));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Description",
                    description,
                    500));

            sqlParams.Add(
                _conn.GenerateInputBool("@IsActive", isActive));

            _conn.ExecuteStoredProcedureNoReturn(
                "WorkAreasUpdate",
                sqlParams.ToArray());
        }


        public DataTable GetUsers()
        {
            List<SqlParameter> sqlParams = new();

            return _conn.ExecuteStoredProcedureAsDataTable(
                "UsersGet",
                sqlParams.ToArray());
        }

        public void InsertUser(
            string firstName,
            string lastName,
            string email,
            string? phoneNumber,
            string role)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@FirstName",
                    firstName,
                    100));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@LastName",
                    lastName,
                    100));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Email",
                    email,
                    320));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@PhoneNumber",
                    phoneNumber,
                    30));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Role",
                    role,
                    50));

            _conn.ExecuteStoredProcedureNoReturn(
                "UsersInsert",
                sqlParams.ToArray());
        }

        public void UpdateUser(
            int id,
            string firstName,
            string lastName,
            string email,
            string? phoneNumber,
            string role,
            bool isActive)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@FirstName",
                    firstName,
                    100));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@LastName",
                    lastName,
                    100));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Email",
                    email,
                    320));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@PhoneNumber",
                    phoneNumber,
                    30));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Role",
                    role,
                    50));

            sqlParams.Add(
                _conn.GenerateInputBool(
                    "@IsActive",
                    isActive));

            _conn.ExecuteStoredProcedureNoReturn(
                "UsersUpdate",
                sqlParams.ToArray());
        }


        public DataTable GetWorkAreaManagers()
        {
            List<SqlParameter> sqlParams = new();

            return _conn.ExecuteStoredProcedureAsDataTable(
                "WorkAreaManagersGet",
                sqlParams.ToArray());
        }

        public void InsertWorkAreaManager(
            int workAreaId,
            int userId,
            bool isPrimary)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@WorkAreaId",
                    workAreaId));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@UserId",
                    userId));

            sqlParams.Add(
                _conn.GenerateInputBool(
                    "@IsPrimary",
                    isPrimary));

            _conn.ExecuteStoredProcedureNoReturn(
                "WorkAreaManagersInsert",
                sqlParams.ToArray());
        }

        public void UpdateWorkAreaManager(
            int id,
            int workAreaId,
            int userId,
            bool isPrimary)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@WorkAreaId",
                    workAreaId));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@UserId",
                    userId));

            sqlParams.Add(
                _conn.GenerateInputBool(
                    "@IsPrimary",
                    isPrimary));

            _conn.ExecuteStoredProcedureNoReturn(
                "WorkAreaManagersUpdate",
                sqlParams.ToArray());
        }

        public DataTable GetWorkers(int? id, string? phoneNumber = null)
        {
            List<SqlParameter> sqlParams = new();
            if (id > 0)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger("@Id", id.Value));
            }
            if (!string.IsNullOrWhiteSpace(phoneNumber))
            {
                sqlParams.Add(
                    _conn.GenerateInputString(
                        "@PhoneNumber",
                        phoneNumber,
                        30));
            }

            return _conn.ExecuteStoredProcedureAsDataTable(
                "WorkersGet",
                sqlParams.ToArray());
        }

        public int InsertWorker(
            string firstName,
            string lastName,
            string phoneNumber,
            string? companyName)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@FirstName",
                    firstName,
                    100));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@LastName",
                    lastName,
                    100));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@PhoneNumber",
                    phoneNumber,
                    30));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@CompanyName",
                    companyName,
                    200));

           return _conn.ExecuteStoredProcedureAsScalarInt(
                "WorkersInsert",
                sqlParams.ToArray());
        }

        public void UpdateWorker(
            int id,
            string firstName,
            string lastName,
            string phoneNumber,
            string? companyName,
            bool isActive)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@FirstName",
                    firstName,
                    100));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@LastName",
                    lastName,
                    100));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@PhoneNumber",
                    phoneNumber,
                    30));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@CompanyName",
                    companyName,
                    200));

            sqlParams.Add(
                _conn.GenerateInputBool(
                    "@IsActive",
                    isActive));

            _conn.ExecuteStoredProcedureNoReturn(
                "WorkersUpdate",
                sqlParams.ToArray());
        }
        public DataRow GetRoomByQrCode(Guid qrCodeIdentifier)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputUniqueIdentifier(
                    "@QrCodeIdentifier",
                    qrCodeIdentifier,
                    100));

            return _conn.ExecuteStoredProcedureAsDataRow(
                "RoomsGetByQrCode",
                sqlParams.ToArray());
        }


        public DataTable GetRooms(int? id, int workAreaId = 0, int siteId = 0)
        {
            List<SqlParameter> sqlParams = new();
            if (id > 0)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger(
                        "@Id",
                        id ?? 0));
            }
            if (workAreaId > 0)
            {
                sqlParams.Add(_conn.GenerateInputInteger("@WorkAreaId", workAreaId));
            }
            if (siteId > 0)
            {
                sqlParams.Add(_conn.GenerateInputInteger("@SiteId", siteId));
            }
            return _conn.ExecuteStoredProcedureAsDataTable(
                "RoomsGet",
                sqlParams.ToArray());
        }

        public void InsertRoom(
            int workAreaId,
            string roomNumber,
            string? name,
            string? description,
            string? locationKind)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@WorkAreaId",
                    workAreaId));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@RoomNumber",
                    roomNumber,
                    100));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Name",
                    name,
                    200));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Description",
                    description,
                    500));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@LocationKind",
                    string.IsNullOrWhiteSpace(locationKind) ? "Apartment" : locationKind,
                    30));

            _conn.ExecuteStoredProcedureNoReturn(
                "RoomsInsert",
                sqlParams.ToArray());
        }

        public void UpdateRoom(
            int id,
            int workAreaId,
            string roomNumber,
            string? name,
            string qrCodeIdentifier,
            string? description,
            string? locationKind,
            bool isActive)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@WorkAreaId",
                    workAreaId));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@RoomNumber",
                    roomNumber,
                    100));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Name",
                    name,
                    200));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Description",
                    description,
                    500));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@LocationKind",
                    locationKind,
                    30));

            sqlParams.Add(
                _conn.GenerateInputBool(
                    "@IsActive",
                    isActive));

            _conn.ExecuteStoredProcedureNoReturn(
                "RoomsUpdate",
                sqlParams.ToArray());
        }


        // ============================================================
        // ACCESS WINDOWS
        // ============================================================

        public DataTable GetAccessWindows()
        {
            List<SqlParameter> sqlParams = new();

            return _conn.ExecuteStoredProcedureAsDataTable(
                "AccessWindowsGet",
                sqlParams.ToArray());
        }

        public void InsertAccessWindow(
            int workAreaId,
            int dayOfWeek,
            TimeSpan startTime,
            TimeSpan endTime)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@WorkAreaId",
                    workAreaId));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@DayOfWeek",
                    dayOfWeek));

            sqlParams.Add(
                _conn.GenerateInputTime(
                    "@StartTime",
                    startTime));

            sqlParams.Add(
                _conn.GenerateInputTime(
                    "@EndTime",
                    endTime));

            _conn.ExecuteStoredProcedureNoReturn(
                "AccessWindowsInsert",
                sqlParams.ToArray());
        }

        public void UpdateAccessWindow(
            int id,
            int workAreaId,
            int dayOfWeek,
            TimeSpan startTime,
            TimeSpan endTime,
            bool isActive)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@WorkAreaId",
                    workAreaId));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@DayOfWeek",
                    dayOfWeek));

            sqlParams.Add(
                _conn.GenerateInputTime(
                    "@StartTime",
                    startTime));

            sqlParams.Add(
                _conn.GenerateInputTime(
                    "@EndTime",
                    endTime));

            sqlParams.Add(
                _conn.GenerateInputBool(
                    "@IsActive",
                    isActive));

            _conn.ExecuteStoredProcedureNoReturn(
                "AccessWindowsUpdate",
                sqlParams.ToArray());
        }


        // ============================================================
        // ACCESS REQUESTS
        // ============================================================

        public DataTable GetAccessRequests(
            int id,
            int workerId,
            int roomId,
            string? status,
            string? search,
            int skip,
            int take,
            int siteId = 0)
        {
            List<SqlParameter> sqlParams = new();

            if (id > 0)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger("@Id", id));
            }
            if (workerId > 0)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger("@WorkerId", workerId));
            }
            if (roomId > 0)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger("@RoomId", roomId));
            }
            if (siteId > 0)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger("@SiteId", siteId));
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                sqlParams.Add(
                    _conn.GenerateInputString("@Status", status, 30));
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                sqlParams.Add(
                    _conn.GenerateInputString("@Search", search, 200));
            }
            sqlParams.Add(
                _conn.GenerateInputInteger("@Skip", skip < 0 ? 0 : skip));
            if (take > 0)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger("@Take", take));
            }

            return _conn.ExecuteStoredProcedureAsDataTable(
                "AccessRequestsGet",
                sqlParams.ToArray());
        }

        public int InsertAccessRequest(
            string phoneNumber,
            int workerId,
            int roomId,
            string reason,
            string workType,
            string? description,
            string? supervisorName,
            DateTime? workFrom,
            DateTime? workTo)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@RoomId",
                    roomId));
            sqlParams.Add(
    _conn.GenerateInputInteger(
        "@WorkerId",
        workerId));
            sqlParams.Add(
    _conn.GenerateInputString(
        "@PhoneNumber",
        phoneNumber,
        500));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Reason",
                    reason,
                    500));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@WorkType",
                    workType,
                    200));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Description",
                    description,
                    1000));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@SupervisorName",
                    supervisorName,
                    200));

            if (workFrom.HasValue)
            {
                sqlParams.Add(_conn.GenerateInputDateTime("@WorkFrom", workFrom.Value));
            }
            if (workTo.HasValue)
            {
                sqlParams.Add(_conn.GenerateInputDateTime("@WorkTo", workTo.Value));
            }

          return  _conn.ExecuteStoredProcedureAsScalarInt("AccessRequestsInsert", sqlParams.ToArray());
        }

        public void UpdateAccessRequest(
            int id,
            int? workerId,
            int? roomId,
            string? status,
            string? reason,
            string? workType,
            string? description,
            DateTime? approvedAt,
            DateTime? rejectedAt,
            DateTime? clockedInAt,
            DateTime? expectedClockOutAt,
            DateTime? clockedOutAt,
            DateTime? completedAt)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id));

            if (workerId.HasValue)
            {
                sqlParams.Add(_conn.GenerateInputInteger("@WorkerId", workerId.Value));
            }

            if (roomId.HasValue)
            {
                sqlParams.Add(_conn.GenerateInputInteger("@RoomId", roomId.Value));
            }

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Status",
                    status,
                    50));
            
            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Reason",
                    reason,
                    500));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@WorkType",
                    workType,
                    200));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Description",
                    description,
                    1000));

            if (approvedAt.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputDateTime(
                        "@ApprovedAt",
                        approvedAt.Value));
            }

            if (rejectedAt.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputDateTime(
                        "@RejectedAt",
                        rejectedAt.Value));
            }

            if (clockedInAt.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputDateTime(
                        "@ClockedInAt",
                        clockedInAt.Value));
            }

            if (expectedClockOutAt.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputDateTime(
                        "@ExpectedClockOutAt",
                        expectedClockOutAt.Value));
            }

            if (clockedOutAt.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputDateTime(
                        "@ClockedOutAt",
                        clockedOutAt.Value));
            }

            if (completedAt.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputDateTime(
                        "@CompletedAt",
                        completedAt.Value));
            }

            _conn.ExecuteStoredProcedureNoReturn(
                "AccessRequestsUpdate",
                sqlParams.ToArray());
        }


        public DataTable GetAccessRequestApprovals(
            int id,
            int accessRequestId,
            int approverUserId,
            string? status,
            string? search,
            int skip,
            int take)
        {
            List<SqlParameter> sqlParams = new();

            if (id > 0)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger("@Id", id));
            }
            if (accessRequestId > 0)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger("@AccessRequestId", accessRequestId));
            }
            if (approverUserId > 0)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger("@ApproverUserId", approverUserId));
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                sqlParams.Add(
                    _conn.GenerateInputString("@Status", status, 30));
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                sqlParams.Add(
                    _conn.GenerateInputString("@Search", search, 200));
            }
            sqlParams.Add(
                _conn.GenerateInputInteger("@Skip", skip < 0 ? 0 : skip));
            if (take > 0)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger("@Take", take));
            }

            return _conn.ExecuteStoredProcedureAsDataTable(
                "AccessRequestApprovalsGet",
                sqlParams.ToArray());
        }
        public DataRow GetRoomByAccessRequestId(int accessRequestId)
        {
            List<SqlParameter> sqlParams = new();
                sqlParams.Add(
        _conn.GenerateInputInteger(
            "@AccessRequestId",
            accessRequestId));

            return _conn.ExecuteStoredProcedureAsDataRow(
                "RoomsGetByAccessRequestId",
                sqlParams.ToArray());
        }


        public void InsertAccessRequestApproval(
            int accessRequestId,
            int approverUserId)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@AccessRequestId",
                    accessRequestId));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@ApproverUserId",
                    approverUserId));

            _conn.ExecuteStoredProcedureNoReturn(
                "AccessRequestApprovalsInsert",
                sqlParams.ToArray());
        }

        public void UpdateAccessRequestApproval(
            int id,
            string status,
            string? comment)
        {
            List<SqlParameter> sqlParams = new();


            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@Id",
                    id));


            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Status",
                    status,
                    50));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Comment",
                    comment,
                    1000));

            _conn.ExecuteStoredProcedureNoReturn(
                "AccessRequestApprovalsUpdate",
                sqlParams.ToArray());
        }

        public DataTable GetAccessPhotos()
        {
            List<SqlParameter> sqlParams = new();

            return _conn.ExecuteStoredProcedureAsDataTable(
                "AccessPhotosGet",
                sqlParams.ToArray());
        }

        public void InsertAccessPhoto(
            int accessRequestId,
            string storagePath,
            string originalFileName,
            string contentType,
            int fileSize,
            int uploadedByWorkerId,
            DateTime capturedAt,
            string photoType)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@AccessRequestId",
                    accessRequestId));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@StoragePath",
                    storagePath,
                    1000));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@OriginalFileName",
                    originalFileName,
                    255));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@ContentType",
                    contentType,
                    100));

            sqlParams.Add(
                _conn.GenerateInputint(
                    "@FileSize",
                    fileSize));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@UploadedByWorkerId",
                    uploadedByWorkerId));

            sqlParams.Add(
                _conn.GenerateInputDateTime(
                    "@CapturedAt",
                    capturedAt));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@PhotoType",
                    photoType,
                    50));

            _conn.ExecuteStoredProcedureNoReturn(
                "AccessPhotosInsert",
                sqlParams.ToArray());
        }

        public void UpdateAccessPhoto(
            int id,
            int accessRequestId,
            string storagePath,
            string originalFileName,
            string contentType,
            int fileSize,
            int uploadedByWorkerId,
            DateTime capturedAt,
            string photoType)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@AccessRequestId",
                    accessRequestId));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@StoragePath",
                    storagePath,
                    1000));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@OriginalFileName",
                    originalFileName,
                    255));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@ContentType",
                    contentType,
                    100));

            sqlParams.Add(
                _conn.GenerateInputint(
                    "@FileSize",
                    fileSize));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@UploadedByWorkerId",
                    uploadedByWorkerId));

            sqlParams.Add(
                _conn.GenerateInputDateTime(
                    "@CapturedAt",
                    capturedAt));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@PhotoType",
                    photoType,
                    50));

            _conn.ExecuteStoredProcedureNoReturn(
                "AccessPhotosUpdate",
                sqlParams.ToArray());
        }


        // ============================================================
        // OTP VERIFICATIONS
        // ============================================================

        public DataTable GetOTPVerifications()
        {
            List<SqlParameter> sqlParams = new();

            return _conn.ExecuteStoredProcedureAsDataTable(
                "OTPVerificationsGet",
                sqlParams.ToArray());
        }
        public DataRow VerifyOTP(int code)
        {
            List<SqlParameter> sqlParams = new();
            sqlParams.Add(_conn.GenerateInputInteger("@Code", code));
            return _conn.ExecuteStoredProcedureAsDataRow("OTPVerify", sqlParams.ToArray());
        }
        public void InsertOTPVerification(
            string phoneNumber,
            int codeHash,
            DateTime expiresAt)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@PhoneNumber",
                    phoneNumber,
                    30));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@CodeHash",
                    codeHash));

            sqlParams.Add(
                _conn.GenerateInputDateTime(
                    "@ExpiresAt",
                    expiresAt));

            _conn.ExecuteStoredProcedureNoReturn(
                "OTPVerificationsInsert",
                sqlParams.ToArray());
        }

        public void UpdateOTPVerification(
            int id,
            int? workerId,
            string phoneNumber,
            string codeHash,
            DateTime expiresAt,
            DateTime? verifiedAt,
            int attemptCount)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id));

            if (workerId.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger(
                        "@WorkerId",
                        workerId.Value));
            }

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@PhoneNumber",
                    phoneNumber,
                    30));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@CodeHash",
                    codeHash,
                    500));

            sqlParams.Add(
                _conn.GenerateInputDateTime(
                    "@ExpiresAt",
                    expiresAt));

            if (verifiedAt.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputDateTime(
                        "@VerifiedAt",
                        verifiedAt.Value));
            }

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@AttemptCount",
                    attemptCount));

            _conn.ExecuteStoredProcedureNoReturn(
                "OTPVerificationsUpdate",
                sqlParams.ToArray());
        }


        // ============================================================
        // NOTIFICATIONS
        // ============================================================

        public DataTable GetNotifications()
        {
            List<SqlParameter> sqlParams = new();

            return _conn.ExecuteStoredProcedureAsDataTable(
                "NotificationsGet",
                sqlParams.ToArray());
        }

        public void InsertNotification(
            int? userId,
            int? workerId,
            int? accessRequestId,
            string type,
            string channel,
            string message)
        {
            List<SqlParameter> sqlParams = new();

            if (userId.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger(
                        "@UserId",
                        userId.Value));
            }

            if (workerId.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger(
                        "@WorkerId",
                        workerId.Value));
            }

            if (accessRequestId.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger(
                        "@AccessRequestId",
                        accessRequestId.Value));
            }

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Type",
                    type,
                    100));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Channel",
                    channel,
                    50));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Message",
                    message,
                    2000));

            _conn.ExecuteStoredProcedureNoReturn(
                "NotificationsInsert",
                sqlParams.ToArray());
        }

        public void UpdateNotification(
            int id,
            int? userId,
            int? workerId,
            int? accessRequestId,
            string type,
            string channel,
            string message,
            string status,
            DateTime? sentAt,
            DateTime? readAt)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id));

            if (userId.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger(
                        "@UserId",
                        userId.Value));
            }

            if (workerId.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger(
                        "@WorkerId",
                        workerId.Value));
            }

            if (accessRequestId.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger(
                        "@AccessRequestId",
                        accessRequestId.Value));
            }

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Type",
                    type,
                    100));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Channel",
                    channel,
                    50));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Message",
                    message,
                    2000));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Status",
                    status,
                    50));

            if (sentAt.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputDateTime(
                        "@SentAt",
                        sentAt.Value));
            }

            if (readAt.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputDateTime(
                        "@ReadAt",
                        readAt.Value));
            }

            _conn.ExecuteStoredProcedureNoReturn(
                "NotificationsUpdate",
                sqlParams.ToArray());
        }


        public DataTable GetAuditEvents()
        {
            List<SqlParameter> sqlParams = new();

            return _conn.ExecuteStoredProcedureAsDataTable(
                "AuditEventsGet",
                sqlParams.ToArray());
        }

        public void InsertAuditEvent(
            int? accessRequestId,
            int? workerId,
            int? userId,
            string eventType,
            string? description
            )
        {
            List<SqlParameter> sqlParams = new();

            if (accessRequestId.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger(
                        "@AccessRequestId",
                        accessRequestId.Value));
            }

            if (workerId.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger(
                        "@WorkerId",
                        workerId.Value));
            }

            if (userId.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger(
                        "@UserId",
                        userId.Value));
            }

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@EventType",
                    eventType,
                    100));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Description",
                    description,
                    2000));


            _conn.ExecuteStoredProcedureNoReturn(
                "AuditEventsInsert",
                sqlParams.ToArray());
        }

        public DataTable GetDepartments(int siteId = 0)
        {
            List<SqlParameter> sqlParams = new();

            if (siteId > 0)
            {
                sqlParams.Add(_conn.GenerateInputInteger("@SiteId", siteId));
            }

            return _conn.ExecuteStoredProcedureAsDataTable(
                "DepartmentsGet",
                sqlParams.ToArray());
        }

        public void InsertDepartment(
            int siteId,
            string name,
            string? description)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@SiteId",
                    siteId));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Name",
                    name,
                    200));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Description",
                    description,
                    500));

            _conn.ExecuteStoredProcedureNoReturn(
                "DepartmentsInsert",
                sqlParams.ToArray());
        }

        public void UpdateDepartment(
            int id,
            int siteId,
            string name,
            string? description,
            bool isActive)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@SiteId",
                    siteId));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Name",
                    name,
                    200));

            sqlParams.Add(
                _conn.GenerateInputString(
                    "@Description",
                    description,
                    500));

            sqlParams.Add(
                _conn.GenerateInputBool(
                    "@IsActive",
                    isActive));

            _conn.ExecuteStoredProcedureNoReturn(
                "DepartmentsUpdate",
                sqlParams.ToArray());
        }

        public DataTable GetDepartmentRoomMappings()
        {
            List<SqlParameter> sqlParams = new();

            return _conn.ExecuteStoredProcedureAsDataTable(
                "DepartmentRoomMappingGet",
                sqlParams.ToArray());
        }

        public void InsertDepartmentRoomMapping(
            int departmentId,
            int roomId)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@DepartmentId",
                    departmentId));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@RoomId",
                    roomId));

            _conn.ExecuteStoredProcedureNoReturn(
                "DepartmentRoomMappingInsert",
                sqlParams.ToArray());
        }

        public void DeleteDepartmentRoomMapping(int id)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id));

            _conn.ExecuteStoredProcedureNoReturn(
                "DepartmentRoomMappingDelete",
                sqlParams.ToArray());
        }

        public DataTable GetDepartmentManagerMappings()
        {
            List<SqlParameter> sqlParams = new();

            return _conn.ExecuteStoredProcedureAsDataTable(
                "DepartmentManagerMappingGet",
                sqlParams.ToArray());
        }

        public void InsertDepartmentManagerMapping(
            int departmentId,
            int userId,
            bool isPrimary)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@DepartmentId",
                    departmentId));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@UserId",
                    userId));

            sqlParams.Add(
                _conn.GenerateInputBool(
                    "@IsPrimary",
                    isPrimary));

            _conn.ExecuteStoredProcedureNoReturn(
                "DepartmentManagerMappingInsert",
                sqlParams.ToArray());
        }

        public void UpdateDepartmentManagerMapping(
            int id,
            int departmentId,
            int userId,
            bool isPrimary)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@DepartmentId",
                    departmentId));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@UserId",
                    userId));

            sqlParams.Add(
                _conn.GenerateInputBool(
                    "@IsPrimary",
                    isPrimary));

            _conn.ExecuteStoredProcedureNoReturn(
                "DepartmentManagerMappingUpdate",
                sqlParams.ToArray());
        }

        public void DeleteDepartmentManagerMapping(int id)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id));

            _conn.ExecuteStoredProcedureNoReturn(
                "DepartmentManagerMappingDelete",
                sqlParams.ToArray());
        }

        public DataTable GetWorkerSiteMappings()
        {
            List<SqlParameter> sqlParams = new();

            return _conn.ExecuteStoredProcedureAsDataTable(
                "WorkerSiteMappingGet",
                sqlParams.ToArray());
        }

        public void InsertWorkerSiteMapping(
            int workerId,
            int siteId)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@WorkerId",
                    workerId));

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@SiteId",
                    siteId));

            _conn.ExecuteStoredProcedureNoReturn(
                "WorkerSiteMappingInsert",
                sqlParams.ToArray());
        }

        public void DeleteWorkerSiteMapping(int id)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id));

            _conn.ExecuteStoredProcedureNoReturn(
                "WorkerSiteMappingDelete",
                sqlParams.ToArray());
        }

        public DataTable GetManagersForRoom(int roomId, int departmentId)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@RoomId",
                    roomId));

            if (departmentId > 0)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger(
                        "@DepartmentId",
                        departmentId));
            }

            return _conn.ExecuteStoredProcedureAsDataTable(
                "ManagersForRoomGet",
                sqlParams.ToArray());
        }

        public DataTable GetDepartmentsForRoom(int roomId)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@RoomId",
                    roomId));

            return _conn.ExecuteStoredProcedureAsDataTable(
                "DepartmentsForRoomGet",
                sqlParams.ToArray());
        }

        public bool ManagerCanApprove(int approverUserId, int? roomId, int? accessRequestId)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputInteger(
                    "@ApproverUserId",
                    approverUserId));

            if (roomId.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger(
                        "@RoomId",
                        roomId.Value));
            }

            if (accessRequestId.HasValue)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger(
                        "@AccessRequestId",
                        accessRequestId.Value));
            }

            return _conn.ExecuteStoredProcedureAsScalarInt(
                "ManagerCanApprove",
                sqlParams.ToArray()) == 1;
        }

        public DataTable GetRoomManagers(int roomId = 0)
        {
            List<SqlParameter> sqlParams = new();
            if (roomId > 0)
            {
                sqlParams.Add(_conn.GenerateInputInteger("@RoomId", roomId));
            }
            return _conn.ExecuteStoredProcedureAsDataTable(
                "RoomManagersGet",
                sqlParams.ToArray());
        }

        public void InsertRoomManager(int roomId, int userId, bool isPrimary)
        {
            List<SqlParameter> sqlParams = new();
            sqlParams.Add(_conn.GenerateInputInteger("@RoomId", roomId));
            sqlParams.Add(_conn.GenerateInputInteger("@UserId", userId));
            sqlParams.Add(_conn.GenerateInputBool("@IsPrimary", isPrimary));
            _conn.ExecuteStoredProcedureNoReturn(
                "RoomManagersInsert",
                sqlParams.ToArray());
        }

        public void UpdateRoomManager(int id, int roomId, int userId, bool isPrimary)
        {
            List<SqlParameter> sqlParams = new();
            sqlParams.Add(_conn.GenerateInputInteger("@Id", id));
            sqlParams.Add(_conn.GenerateInputInteger("@RoomId", roomId));
            sqlParams.Add(_conn.GenerateInputInteger("@UserId", userId));
            sqlParams.Add(_conn.GenerateInputBool("@IsPrimary", isPrimary));
            _conn.ExecuteStoredProcedureNoReturn(
                "RoomManagersUpdate",
                sqlParams.ToArray());
        }

        public DateTime? GetAccessRequestWorkTo(int id)
        {
            if (id < 1) return null;
            DataTable dt = GetAccessRequests(id, 0, 0, null, null, 0, 1);
            if (dt.Rows.Count == 0 || !dt.Columns.Contains("WorkTo") || dt.Rows[0]["WorkTo"] == DBNull.Value)
            {
                return null;
            }
            return Convert.ToDateTime(dt.Rows[0]["WorkTo"]);
        }
    }
}
