using Microsoft.Data.SqlClient;
using System.Data;
using Twilio.Rest.Voice.V1;
using static AccessControl.Model.Model;

namespace AccessControl.Database
{
    public partial class DatabaseLogic
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
            string? reference)
        {
            List<SqlParameter> sqlParams = new();

            sqlParams.Add(
                _conn.GenerateInputString("@Name", name, 200));

            sqlParams.Add(
                _conn.GenerateInputString("@Address", address, 500));

            sqlParams.Add(
                _conn.GenerateInputString("@Reference", reference, 100));

            _conn.ExecuteStoredProcedureNoReturn(
                "SitesInsert",
                sqlParams.ToArray());
        }

        public void UpdateSite(
            int id,
            string name,
            string? address,
            string? reference,
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
                _conn.GenerateInputBool("@IsActive", isActive));

            _conn.ExecuteStoredProcedureNoReturn(
                "SitesUpdate",
                sqlParams.ToArray());
        }


        // ============================================================
        // WORK AREAS
        // ============================================================

        public DataTable GetWorkAreas()
        {
            List<SqlParameter> sqlParams = new();

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

        public DataTable GetWorkers(int? id)
        {
            List<SqlParameter> sqlParams = new();
            if (id > 0)
            {

            sqlParams.Add(
                _conn.GenerateInputInteger("@Id", id.Value ));
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


        public DataTable GetRooms(int? id)
        {
            List<SqlParameter> sqlParams = new();
            if(id > 0)
            {
                sqlParams.Add(
                    _conn.GenerateInputInteger(
                        "@Id",
                        id ?? 0));
            }
            return _conn.ExecuteStoredProcedureAsDataTable(
                "RoomsGet",
                sqlParams.ToArray());
        }

        public void InsertRoom(
            int workAreaId,
            string roomNumber,
            string? name,
            string? description)
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
                    "@QrCodeIdentifier",
                    qrCodeIdentifier,
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

        public DataTable GetAccessRequests()
        {
            List<SqlParameter> sqlParams = new();

            return _conn.ExecuteStoredProcedureAsDataTable(
                "AccessRequestsGet",
                sqlParams.ToArray());
        }

        public int InsertAccessRequest(string phoneNumber, int workerId, int roomId, string reason, string workType, string? description)
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


        public DataTable GetAccessRequestApprovals()
        {
            List<SqlParameter> sqlParams = new();

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
    }
}
