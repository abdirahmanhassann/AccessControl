using AccessControl.Database;
using AccessControl.Helpers;
using AccessControl.Security;
using Microsoft.IdentityModel.Abstractions;
using System.Data;
using System.Reflection;
using static AccessControl.Helpers.ApiHelper;
using static AccessControl.Model.ApiResponses;
using static AccessControl.Model.Model;

namespace AccessControl.Processor
{

    public class AccessProcessor
    {
        private IConfiguration _config;
        private DatabaseLogic _db;

        public AccessProcessor(IConfiguration config)
        {
            _config = config;
            _db = new(
                config.GetSection("ConnectionStrings:AccessControlDB").Get<string>()!,
                false);
        }


        // ============================================================
        // SITES
        // ============================================================
        public GenericResponse Login(string email, string password)
        {
            try
            {

                // ###TODO: log IP address? (for multiple account detection, new device login check)
                // ###TODO: log browser agent? (for multiple account detection, new device login check)

                DataRow dr = _db.GetAccountByEmail(email);
                if (dr == null)
                {
                    return CreateFailureResponse("Either the email specified is not signed up, or the password is incorrect");
                }

                Encryptor enc = new(_config);
                string salt = dr.Field<string>("Salt")!;
                string pwdEncrypted = enc.Encrypt(password, salt);
                if (pwdEncrypted != dr.Field<string>("Password"))
                {
                    return CreateFailureResponse("Either the email specified is not signed up, or the password is incorrect");
                }

                string token = Guid.NewGuid().ToString();
                _db.AddAccessToken(dr.Field<int>("Id"), token);

                return CreateSuccessResponseWithData(token);
            }
            catch (Exception ex)
            {
                // We do not have a userId at this point, so log under account 0
                _db.LogError(MethodBase.GetCurrentMethod()?.Module + "/" + MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace, 0);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse SignUp(string email, string firstName, string lastName, string password, string phoneNumber)
        {
            try
            {
                // ###TODO check for profanity in displayName or Shop
                Encryptor enc = new(_config);
                string salt = enc.GenerateRandomSalt();
                string pwdEncrypted = enc.Encrypt(password, salt);

                _db.Signup(email, firstName, lastName, pwdEncrypted, salt, phoneNumber);
                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                // We do not have a userId at this point, so log under account 0
                _db.LogError(MethodBase.GetCurrentMethod()?.Module + "/" + MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace, 0);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse GetSites(int userId)
        {
            try
            {
                DataTable dt = _db.GetSites();

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse InsertSite(
            int userId,
            string name,
            string? address,
            string? reference)
        {
            try
            {
                _db.InsertSite(
                    name,
                    address,
                    reference);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse UpdateSite(
            int userId,
            int id,
            string name,
            string? address,
            string? reference,
            bool isActive)
        {
            try
            {
                _db.UpdateSite(
                    id,
                    name,
                    address,
                    reference,
                    isActive);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }


        // ============================================================
        // WORK AREAS
        // ============================================================

        public GenericResponse GetWorkAreas(int userId)
        {
            try
            {
                DataTable dt = _db.GetWorkAreas();

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse InsertWorkArea(
            int userId,
            int siteId,
            string name,
            string? description)
        {
            try
            {
                _db.InsertWorkArea(
                    siteId,
                    name,
                    description);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse UpdateWorkArea(
            int userId,
            int id,
            int siteId,
            string name,
            string? description,
            bool isActive)
        {
            try
            {
                _db.UpdateWorkArea(
                    id,
                    siteId,
                    name,
                    description,
                    isActive);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }


        // ============================================================
        // USERS
        // ============================================================

        public GenericResponse GetUsers(int userId)
        {
            try
            {
                DataTable dt = _db.GetUsers();
                if (userId < 1)
                {
                    DataTable managers = dt.Clone();
                    foreach (DataRow row in dt.Rows)
                    {
                        string role = row["Role"]?.ToString() ?? "";
                        bool active = row["IsActive"] is bool flag ? flag : true;
                        if (!active) continue;
                        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                            || role.Equals("Manager", StringComparison.OrdinalIgnoreCase)
                            || role.Equals("SiteManager", StringComparison.OrdinalIgnoreCase))
                        {
                            managers.ImportRow(row);
                        }
                    }
                    dt = managers;
                }

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse InsertUser(
            int userId,
            string firstName,
            string lastName,
            string email,
            string? phoneNumber,
            string role)
        {
            try
            {
                _db.InsertUser(
                    firstName,
                    lastName,
                    email,
                    phoneNumber,
                    role);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse UpdateUser(
            int userId,
            int id,
            string firstName,
            string lastName,
            string email,
            string? phoneNumber,
            string role,
            bool isActive)
        {
            try
            {
                _db.UpdateUser(
                    id,
                    firstName,
                    lastName,
                    email,
                    phoneNumber,
                    role,
                    isActive);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }


        // ============================================================
        // WORK AREA MANAGERS
        // ============================================================

        public GenericResponse GetWorkAreaManagers(int userId)
        {
            try
            {
                DataTable dt = _db.GetWorkAreaManagers();

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse InsertWorkAreaManager(
            int userId,
            int workAreaId,
            int managerUserId,
            bool isPrimary)
        {
            try
            {
                _db.InsertWorkAreaManager(
                    workAreaId,
                    managerUserId,
                    isPrimary);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse UpdateWorkAreaManager(
            int userId,
            int id,
            int workAreaId,
            int managerUserId,
            bool isPrimary)
        {
            try
            {
                _db.UpdateWorkAreaManager(
                    id,
                    workAreaId,
                    managerUserId,
                    isPrimary);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }


        // ============================================================
        // WORKERS
        // ============================================================

        public GenericResponse GetWorkers(int userId)
        {
            try
            {
                DataTable dt = _db.GetWorkers(null);

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse InsertWorker(
            string firstName,
            string lastName,
            string phoneNumber,
            string? companyName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(phoneNumber))
                {
                    return CreateFailureResponse("Phone number is required.");
                }

                DataTable existing = _db.GetWorkers(null, phoneNumber);
                if (existing.Rows.Count > 0)
                {
                    return CreateSuccessResponseWithData(existing);
                }

                if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                {
                    return CreateFailureResponse("First name and last name are required for a new worker.");
                }

                int workerId = _db.InsertWorker(
                    firstName.Trim(),
                    lastName.Trim(),
                    phoneNumber.Trim(),
                    companyName);

                DataTable created = _db.GetWorkers(workerId);
                return CreateSuccessResponseWithData(created);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    0);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse UpdateWorker(
            int userId,
            int id,
            string firstName,
            string lastName,
            string phoneNumber,
            string? companyName,
            bool isActive)
        {
            try
            {
                _db.UpdateWorker(
                    id,
                    firstName,
                    lastName,
                    phoneNumber,
                    companyName,
                    isActive);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }


        // ============================================================
        // ROOMS
        // ============================================================

        public GenericResponse GetRooms(int userId)
        {
            try
            {
                DataTable dt = _db.GetRooms(null);

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }
        public GenericResponse ScanQrCode(Guid qrCodeIdentifier)
        {
            try
            {
                DataRow dr = _db.GetRoomByQrCode(qrCodeIdentifier);

                if (dr == null)
                {
                    return CreateFailureResponse(
                        "The QR code is invalid or the room is no longer active.");
                }

                return CreateSuccessResponseWithData(dr);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    0);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }


        public GenericResponse InsertRoom(
            int userId,
            int workAreaId,
            string roomNumber,
            string? name,
            string? description)
        {
            try
            {
                _db.InsertRoom(
                    workAreaId,
                    roomNumber,
                    name,
                    description);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse UpdateRoom(
            int userId,
            int id,
            int workAreaId,
            string roomNumber,
            string? name,
            string qrCodeIdentifier,
            string? description,
            bool isActive)
        {
            try
            {
                _db.UpdateRoom(
                    id,
                    workAreaId,
                    roomNumber,
                    name,
                    qrCodeIdentifier,
                    description,
                    isActive);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }


        // ============================================================
        // ACCESS WINDOWS
        // ============================================================

        public GenericResponse GetAccessWindows(int userId)
        {
            try
            {
                DataTable dt = _db.GetAccessWindows();

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse InsertAccessWindow(
            int userId,
            int workAreaId,
            int dayOfWeek,
            TimeSpan startTime,
            TimeSpan endTime)
        {
            try
            {
                _db.InsertAccessWindow(
                    workAreaId,
                    dayOfWeek,
                    startTime,
                    endTime);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse UpdateAccessWindow(
            int userId,
            int id,
            int workAreaId,
            int dayOfWeek,
            TimeSpan startTime,
            TimeSpan endTime,
            bool isActive)
        {
            try
            {
                _db.UpdateAccessWindow(
                    id,
                    workAreaId,
                    dayOfWeek,
                    startTime,
                    endTime,
                    isActive);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse GetAccessRequests(int userId)
        {
            try
            {
                DataTable dt = _db.GetAccessRequests();

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public async Task<GenericResponse> InsertAccessRequest(
            string phoneNumber,
            int workerId,
            int roomId,
            string reason,
            string workType,
            string? description,
            int approverUserId)
        {
            try
            {
                if (approverUserId < 1)
                {
                    return CreateFailureResponse("A manager must be selected to approve this request.");
                }
                if (!_db.ManagerCanApprove(approverUserId, roomId, null))
                {
                    return CreateFailureResponse("That manager is not assigned to the department and site for that room.");
                }

                int dr = _db.InsertAccessRequest(
                    phoneNumber,
                    workerId,
                    roomId,
                    reason,
                    workType,
                    description);
                _db.InsertAccessRequestApproval(dr, approverUserId);
                _db.InsertAuditEvent(
                    dr,
                    workerId,
                    approverUserId,
                    "AccessRequested",
                    $"Worker #{workerId} submitted access request #{dr} for manager #{approverUserId}.");

                DataTable dt = _db.GetWorkers(workerId);
                DataTable dt2 = _db.GetRooms(roomId);
                string firstname = "", lastname = "", sitename = "", workareaname = "", roomname = "";
                if (dt.Rows.Count > 0)
                {
                    firstname = dt.Rows[0].Field<string>("FirstName") ?? "";
                    lastname = dt.Rows[0].Field<string>("LastName") ?? "";
                }
                if (dt2.Rows.Count > 0)
                {
                    sitename = dt2.Rows[0].Field<string>("SiteName") ?? "";
                    workareaname = dt2.Rows[0].Field<string>("WorkAreaName") ?? "";
                    roomname = dt2.Rows[0].Field<string>("RoomName") ?? dt2.Rows[0].Field<string>("RoomName") ?? "";
                }

                try
                {
                    TwilioHelper twilio = new();
                    await twilio.ApprovalRequestAsync(firstname, lastname, sitename, workareaname, roomname, phoneNumber);
                }
                catch (Exception smsEx)
                {
                    _db.LogError(
                        MethodBase.GetCurrentMethod()?.Module + "/" +
                        MethodBase.GetCurrentMethod()?.Name!,
                        "Twilio SMS skipped: " + smsEx.Message,
                        0);
                }
                return CreateSuccessResponseWithData(dr);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    0);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse UpdateAccessRequest(
            int userId,
            int id,
            int workerId,
            int roomId,
            string status,
            string reason,
            string workType,
            string? description,
            DateTime? approvedAt,
            DateTime? rejectedAt,
            DateTime? clockedInAt,
            DateTime? expectedClockOutAt,
            DateTime? clockedOutAt,
            DateTime? completedAt)
        {
            try
            {
                _db.UpdateAccessRequest(
                    id,
                    workerId,
                    roomId,
                    status,
                    reason,
                    workType,
                    description,
                    approvedAt,
                    rejectedAt,
                    clockedInAt,
                    expectedClockOutAt,
                    clockedOutAt,
                    completedAt);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }
        public GenericResponse GetAccessRequestApprovals(int userId)
        {
            try
            {
                DataTable dt = _db.GetAccessRequestApprovals();

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse InsertAccessRequestApproval(
            int userId,
            int accessRequestId,
            int approverUserId)
        {
            try
            {
                _db.InsertAccessRequestApproval(
                    accessRequestId,
                    approverUserId);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public async Task<GenericResponse> UpdateAccessRequestApproval(
            int id,
            int accessRequestId,
            int approverUserId,
            int? workerId,
            int? roomId,
            string status,
            string? comment,
            string? reason,
            string? workType,
            string? description,
            DateTime? approvedAt,
            DateTime? rejectedAt)
        {
            try
            {
                bool approved = string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase);
                bool rejected = string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase);
                bool cancelled = string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
                string normalized =
                    approved ? "Approved" :
                    rejected ? "Rejected" :
                    cancelled ? "Cancelled" :
                    status;

                if (!_db.ManagerCanApprove(approverUserId, roomId, accessRequestId))
                {
                    return CreateFailureResponse("You are not assigned to the department and site for this room.");
                }

                _db.UpdateAccessRequestApproval(
                    id,
                    normalized,
                    comment);

                if (accessRequestId > 0)
                {
                    _db.UpdateAccessRequest(
                        accessRequestId,
                        workerId,
                        roomId,
                        normalized,
                        reason,
                        workType,
                        description,
                        approved ? DateTime.Now : approvedAt,
                        rejected ? DateTime.Now : rejectedAt,
                        approved ? DateTime.Now : null,
                        null,
                        null,
                        null);
                }

                _db.InsertAuditEvent(
                    accessRequestId > 0 ? accessRequestId : null,
                    workerId,
                    approverUserId,
                    normalized,
                    $"Manager #{approverUserId} marked request #{accessRequestId} as {normalized}.");

                DataRow dr = _db.GetRoomByAccessRequestId(accessRequestId);
                if (dr != null)
                {
                    string phone = dr.Table.Columns.Contains("PhoneNumber") ? dr.Field<string>("PhoneNumber") ?? "" : "";
                    string roomName = dr.Table.Columns.Contains("Name") ? dr.Field<string>("Name") ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(phone))
                    {
                        try
                        {
                            TwilioHelper twilio = new();
                            await twilio.StatusUpdateAsync(normalized.ToLowerInvariant(), phone, roomName, comment ?? "");
                        }
                        catch (Exception smsEx)
                        {
                            _db.LogError(
                                MethodBase.GetCurrentMethod()?.Module + "/" +
                                MethodBase.GetCurrentMethod()?.Name!,
                                "Twilio SMS skipped: " + smsEx.Message,
                                approverUserId);
                        }
                    }
                }
                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    approverUserId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }
        public GenericResponse GetAccessPhotos(int userId)
        {
            try
            {
                DataTable dt = _db.GetAccessPhotos();

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse InsertAccessPhoto(
            int userId,
            int accessRequestId,
            string storagePath,
            string originalFileName,
            string contentType,
            int fileSize,
            int uploadedByWorkerId,
            DateTime capturedAt,
            string photoType)
        {
            try
            {
                _db.InsertAccessPhoto(
                    accessRequestId,
                    storagePath,
                    originalFileName,
                    contentType,
                    fileSize,
                    uploadedByWorkerId,
                    capturedAt,
                    photoType);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse UpdateAccessPhoto(
            int userId,
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
            try
            {
                _db.UpdateAccessPhoto(
                    id,
                    accessRequestId,
                    storagePath,
                    originalFileName,
                    contentType,
                    fileSize,
                    uploadedByWorkerId,
                    capturedAt,
                    photoType);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }


        // ============================================================
        // OTP VERIFICATIONS
        // ============================================================

        public GenericResponse GetOTPVerifications(int userId)
        {
            try
            {
                DataTable dt = _db.GetOTPVerifications();

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }
        public GenericResponse VerifyOTP(int code)
        {
            try
            {
                DataRow dr = _db.VerifyOTP(code);
                if (dr == null || dr.Field<int>("value") != 1)
                {
                    return CreateFailureResponse("Wrong Code provided");
                }

                string? phone = null;
                if (dr.Table.Columns.Contains("PhoneNumber"))
                {
                    phone = dr.Field<string>("PhoneNumber");
                }

                if (!string.IsNullOrWhiteSpace(phone))
                {
                    DataTable workers = _db.GetWorkers(null, phone);
                    if (workers.Rows.Count > 0)
                    {
                        return CreateSuccessResponseWithData(workers);
                    }
                }

                return CreateSuccessResponseWithData(dr);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    0);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }


        public async Task<GenericResponse> InsertOTPVerification(
           string phoneNumber,
            DateTime expiresAt)
        {
            try
            {
                TwilioHelper twilio = new();
                int code = ApiHelper.GenerateNumericOtp();
                //COMMENTED OOUT FOR TESTING PURPOSES. DONT WANNA WASTE TEXT CREDITS
               // await twilio.SendMessageAsync(phoneNumber, code);

                _db.InsertOTPVerification(
                    phoneNumber,
                    code,
                    expiresAt);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace, 0);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse UpdateOTPVerification(
            int userId,
            int id,
            int? workerId,
            string phoneNumber,
            string codeHash,
            DateTime expiresAt,
            DateTime? verifiedAt,
            int attemptCount)
        {
            try
            {
                _db.UpdateOTPVerification(
                    id,
                    workerId,
                    phoneNumber,
                    codeHash,
                    expiresAt,
                    verifiedAt,
                    attemptCount);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }


        // ============================================================
        // NOTIFICATIONS
        // ============================================================

        public GenericResponse GetNotifications(int userId)
        {
            try
            {
                DataTable dt = _db.GetNotifications();

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse InsertNotification(
            int userId,
            int? notificationUserId,
            int? workerId,
            int? accessRequestId,
            string type,
            string channel,
            string message)
        {
            try
            {
                _db.InsertNotification(
                    notificationUserId,
                    workerId,
                    accessRequestId,
                    type,
                    channel,
                    message);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse UpdateNotification(
            int userId,
            int id,
            int? notificationUserId,
            int? workerId,
            int? accessRequestId,
            string type,
            string channel,
            string message,
            string status,
            DateTime? sentAt,
            DateTime? readAt)
        {
            try
            {
                _db.UpdateNotification(
                    id,
                    notificationUserId,
                    workerId,
                    accessRequestId,
                    type,
                    channel,
                    message,
                    status,
                    sentAt,
                    readAt);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }


        // ============================================================
        // AUDIT EVENTS
        // ============================================================

        public GenericResponse GetAuditEvents(int userId)
        {
            try
            {
                DataTable dt = _db.GetAuditEvents();

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse InsertAuditEvent(
            int userId,
            int? accessRequestId,
            int? workerId,
            int? eventUserId,
            string eventType,
            string? description
            )
        {
            try
            {
                _db.InsertAuditEvent(
                    accessRequestId,
                    workerId,
                    eventUserId,
                    eventType,
                    description
                    );

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        // ============================================================
        // DEPARTMENTS
        // ============================================================

        public GenericResponse GetDepartments(int userId)
        {
            try
            {
                DataTable dt = _db.GetDepartments();

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse InsertDepartment(
            int userId,
            int siteId,
            string name,
            string? description)
        {
            try
            {
                _db.InsertDepartment(
                    siteId,
                    name,
                    description);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse UpdateDepartment(
            int userId,
            int id,
            int siteId,
            string name,
            string? description,
            bool isActive)
        {
            try
            {
                _db.UpdateDepartment(
                    id,
                    siteId,
                    name,
                    description,
                    isActive);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse GetDepartmentRoomMappings(int userId)
        {
            try
            {
                DataTable dt = _db.GetDepartmentRoomMappings();

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse InsertDepartmentRoomMapping(
            int userId,
            int departmentId,
            int roomId)
        {
            try
            {
                _db.InsertDepartmentRoomMapping(
                    departmentId,
                    roomId);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse DeleteDepartmentRoomMapping(
            int userId,
            int id)
        {
            try
            {
                _db.DeleteDepartmentRoomMapping(id);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse GetDepartmentManagerMappings(int userId)
        {
            try
            {
                DataTable dt = _db.GetDepartmentManagerMappings();

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse InsertDepartmentManagerMapping(
            int userId,
            int departmentId,
            int managerUserId,
            bool isPrimary)
        {
            try
            {
                _db.InsertDepartmentManagerMapping(
                    departmentId,
                    managerUserId,
                    isPrimary);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse UpdateDepartmentManagerMapping(
            int userId,
            int id,
            int departmentId,
            int managerUserId,
            bool isPrimary)
        {
            try
            {
                _db.UpdateDepartmentManagerMapping(
                    id,
                    departmentId,
                    managerUserId,
                    isPrimary);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse DeleteDepartmentManagerMapping(
            int userId,
            int id)
        {
            try
            {
                _db.DeleteDepartmentManagerMapping(id);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse GetWorkerSiteMappings(int userId)
        {
            try
            {
                DataTable dt = _db.GetWorkerSiteMappings();

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse InsertWorkerSiteMapping(
            int userId,
            int workerId,
            int siteId)
        {
            try
            {
                _db.InsertWorkerSiteMapping(
                    workerId,
                    siteId);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse DeleteWorkerSiteMapping(
            int userId,
            int id)
        {
            try
            {
                _db.DeleteWorkerSiteMapping(id);

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    userId);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse GetManagersForRoom(int roomId, int departmentId)
        {
            try
            {
                DataTable dt = _db.GetManagersForRoom(roomId, departmentId);

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    0);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse GetDepartmentsForRoom(int roomId)
        {
            try
            {
                DataTable dt = _db.GetDepartmentsForRoom(roomId);

                return CreateSuccessResponseWithData(dt);
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    0);

                return CreateFailureResponse(GENERIC_ERROR);
            }
        }