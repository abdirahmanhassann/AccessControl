using AccessControl.Database;
using AccessControl.Helpers;
using AccessControl.Security;
using System.Data;
using System.Reflection;
using static AccessControl.Helpers.ApiHelper;
using static AccessControl.Model.ApiResponses;

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
              int workerId =  _db.InsertWorker(
                    firstName,
                    lastName,
                    phoneNumber,
                    companyName);

                return CreateSuccessResponseWithData(workerId);
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
        public GenericResponse ScanQrCode(string qrCodeIdentifier)
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


        // ============================================================
        // ACCESS REQUESTS
        // ============================================================

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
            string? description)
        {
            try
            {
              int dr =  _db.InsertAccessRequest(
                    phoneNumber,
                    workerId,
                    roomId,
                    reason,
                    workType,
                    description);
                //Allow functionality to allow any manager to approve
                _db.InsertAccessRequestApproval(dr,1);
                DataTable dt = _db.GetWorkers(workerId);
                DataTable dt2 = _db.GetRooms(roomId);
                string firstname = "", lastname = "", sitename = "", workareaname = "", roomname = "";
                if(dt.Rows.Count > 0)
                {
                    firstname = dt.Rows[0].Field<string>("FirstName")!;
                    lastname = dt.Rows[0].Field<string>("LastName")!;

                }
                if(dt2.Rows.Count > 0)
                {
                    sitename = dt2.Rows[0].Field<string>("SiteName")!;
                    workareaname = dt2.Rows[0].Field<string>("WorkAreaName")!;
                    roomname = dt2.Rows[0].Field<string>("RoomName")!;
                }

                TwilioHelper twilio = new();
                await twilio.ApprovalRequestAsync(firstname,lastname,sitename,workareaname,roomname,phoneNumber);
                return CreateSuccessResponseNoData();
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


        // ============================================================
        // ACCESS REQUEST APPROVALS
        // ============================================================

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
            int accessRequestId,
            int approverUserId,
            string status,
            string? comment)
        {
            try
            {
                _db.UpdateAccessRequestApproval(
                    accessRequestId,
                    status,
                    comment);

                DataRow dr = _db.GetRoomByAccessRequestId(accessRequestId);
                TwilioHelper twilio = new();

                await twilio.StatusUpdateAsync(status, dr.Field<string>("PhoneNumber")!.ToString(), dr.Field<string>("Name")!.ToString(), comment);
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


        // ============================================================
        // ACCESS PHOTOS
        // ============================================================

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
                if (dr.Field<int>("value")! == 1)
                {
                return CreateSuccessResponseWithData(dr);
                }
                 return CreateFailureResponse("Wrong Code provided");
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
                await twilio.SendMessageAsync(phoneNumber, code);

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
            string? description,
            string? ipAddress,
            string? metadata)
        {
            try
            {
                _db.InsertAuditEvent(
                    accessRequestId,
                    workerId,
                    eventUserId,
                    eventType,
                    description,
                    ipAddress,
                    metadata);

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
    }
}