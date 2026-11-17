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
        private IWebHostEnvironment _env;
        private bool _twilio;
        public AccessProcessor(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;
            _db = new(
                config.GetSection("ConnectionStrings:AccessControlDB").Get<string>()!,
                false);
            _twilio = config.GetSection("Twilio").Get<bool>()!;
        }


        public GenericResponse Login(string email, string password)
        {
            try
            {
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

        public GenericResponse SignUp(string email, string firstName, string lastName, string password, string phoneNumber, string role = "Manager")
        {
            try
            {
                // ###TODO check for profanity in displayName
                Encryptor enc = new(_config);
                string salt = enc.GenerateRandomSalt();
                string pwdEncrypted = enc.Encrypt(password, salt);

                _db.Signup(email, firstName, lastName, pwdEncrypted, salt, phoneNumber, role);
                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(MethodBase.GetCurrentMethod()?.Module + "/" + MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace, 0);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public async Task<GenericResponse> RequestPasswordReset(string email)
        {
            try
            {
                DataRow user = _db.GetAccountByEmail(email);
                if (user != null)
                {
                    string phone = user.Table.Columns.Contains("PhoneNumber")
                        ? user.Field<string>("PhoneNumber") ?? ""
                        : "";
                    if (!string.IsNullOrWhiteSpace(phone))
                    {
                        int code = ApiHelper.GenerateNumericOtp();
                        _db.InsertOTPVerification(phone, code, DateTime.Now.AddMinutes(10));
                        try
                        {
                            TwilioHelper twilio = new();
                            if (_twilio)
                            {

                                await twilio.SendPlainMessageAsync(
                                    phone,
                                    $"SiteGate password reset code: {code:D6}. It expires in 10 minutes.");
                            }
                        }
                        catch (Exception smsEx)
                        {
                            _db.LogError(
                                MethodBase.GetCurrentMethod()?.Module + "/" +
                                MethodBase.GetCurrentMethod()?.Name!,
                                "Twilio SMS skipped: " + smsEx.Message,
                                0);
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
                    0);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse ResetPassword(string email, int code, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                {
                    return CreateFailureResponse("Password must be at least 8 characters.");
                }
                DataRow user = _db.GetAccountByEmail(email);
                if (user == null)
                {
                    return CreateFailureResponse("That reset code is not valid.");
                }
                DataRow otp = _db.VerifyOTP(code);
                bool ok = otp != null && otp.Table.Columns.Contains("value") && Convert.ToInt32(otp["value"]) == 1;
                if (!ok && otp != null && otp.Table.Columns.Contains("Value"))
                {
                    ok = Convert.ToInt32(otp["Value"]) == 1;
                }
                if (!ok)
                {
                    return CreateFailureResponse("That reset code is not valid or has expired.");
                }
                int userId = Convert.ToInt32(user["Id"]);
                Encryptor enc = new(_config);
                string salt = enc.GenerateRandomSalt();
                _db.SetUserPassword(userId, enc.Encrypt(password, salt), salt);
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

        public GenericResponse SetUserPassword(int actorId, int userId, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                {
                    return CreateFailureResponse("Password must be at least 8 characters.");
                }
                Encryptor enc = new(_config);
                string salt = enc.GenerateRandomSalt();
                _db.SetUserPassword(userId, enc.Encrypt(password, salt), salt);
                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    actorId);
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
            string? reference,
            string? qrCodeIdentifier)
        {
            try
            {
                _db.InsertSite(
                    name,
                    address,
                    reference,
                    qrCodeIdentifier);

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
            string? qrCodeIdentifier,
            bool isActive)
        {
            try
            {
                _db.UpdateSite(
                    id,
                    name,
                    address,
                    reference,
                    qrCodeIdentifier,
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

        public GenericResponse GetWorkAreas(int userId, int siteId = 0)
        {
            try
            {
                DataTable dt = _db.GetWorkAreas(siteId);

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
                        if (IsManagerRole(role))
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
            string role,
            string? password = null)
        {
            try
            {
                Encryptor enc = new(_config);
                string salt = enc.GenerateRandomSalt();
                string plain = string.IsNullOrWhiteSpace(password) ? Guid.NewGuid().ToString() : password;
                _db.InsertUser(
                    firstName,
                    lastName,
                    email,
                    phoneNumber,
                    role,
                    enc.Encrypt(plain, salt),
                    salt);

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
                DataTable dt = _db.GetWorkers(userId,null);

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

                DataTable created = _db.GetWorkers(workerId,null);
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
        public GenericResponse GetRooms(int userId, int workAreaId = 0, int siteId = 0)
        {
            try
            {
                DataTable dt = _db.GetRooms(null, workAreaId, siteId);

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
                if (string.IsNullOrWhiteSpace(qrCodeIdentifier))
                {
                    return CreateFailureResponse("QR code is required.");
                }

                string code = qrCodeIdentifier.Trim();
                DataRow site = _db.GetSiteByQrCode(code);
                if (site != null)
                {
                    return CreateSuccessResponseWithData(site);
                }

                if (Guid.TryParse(code, out Guid roomQr))
                {
                    DataRow room = _db.GetRoomByQrCode(roomQr);
                    if (room != null)
                    {
                        return CreateSuccessResponseWithData(room);
                    }
                }

                return CreateFailureResponse(
                    "The QR code is invalid or the site is no longer active.");
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
            string? description,
            string? locationKind)
        {
            try
            {
                _db.InsertRoom(
                    workAreaId,
                    roomNumber,
                    name,
                    description,
                    locationKind);

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
            string? locationKind,
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
                    locationKind,
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
        public bool WorkerOwnsOpenRequest(
            int accessRequestId,
            int workerId)
        {
            DataRow? row = WorkerRequestRow(accessRequestId, workerId);

            if (row == null)
            {
                return false;
            }

            string status = row.Table.Columns.Contains("Status") && row["Status"] != DBNull.Value
                ? Convert.ToString(row["Status"]) ?? ""
                : "";

            if (!status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // An already closed visit is not the worker's to add photos to.
            return !row.Table.Columns.Contains("CompletedAt") ||
                row["CompletedAt"] == DBNull.Value;
        }
        public bool WorkerOwnsRequest(
            int accessRequestId,
            int workerId)
        {
            return WorkerRequestRow(accessRequestId, workerId) != null;
        }

        private DataRow? WorkerRequestRow(
            int accessRequestId,
            int workerId)
        {
            try
            {
                if (accessRequestId < 1 || workerId < 1)
                {
                    return null;
                }

                DataTable dt = _db.GetAccessRequests(
                    accessRequestId,
                    workerId,
                    0,
                    null,
                    null,
                    0,
                    1);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return null;
                }

                DataRow row = dt.Rows[0];

                if (!dt.Columns.Contains("WorkerId") ||
                    row["WorkerId"] == DBNull.Value ||
                    Convert.ToInt32(row["WorkerId"]) != workerId)
                {
                    return null;
                }

                return row;
            }
            catch (Exception ex)
            {
                _db.LogError(
                    MethodBase.GetCurrentMethod()?.Module + "/" +
                    MethodBase.GetCurrentMethod()?.Name!,
                    ex.Message + " " + ex.StackTrace,
                    0);

                return null;
            }
        }

        public GenericResponse GetAccessRequests(
            int userId,
            int id,
            int workerId,
            int roomId,
            string? status,
            string? search,
            int skip,
            int take,
            int siteId = 0)
        {
            try
            {
                DataTable dt = _db.GetAccessRequests(
                    id,
                    workerId,
                    roomId,
                    status,
                    search,
                    skip,
                    take,
                    siteId);

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
            int approverUserId,
            string? supervisorName,
            DateTime? workFrom,
            DateTime? workTo,
            int workAreaId = 0,
            string? roomText = null,
            int departmentId = 0,
            string? locationKind = null,
            int siteId = 0)
        {
            try
            {
                if (approverUserId < 1)
                {
                    return CreateFailureResponse("A manager must be selected to approve this request.");
                }

                int towerId = ResolveWorkAreaId(workAreaId, siteId);
                int resolvedRoomId = ResolveRoomId(roomId, towerId, roomText, locationKind);
                if (resolvedRoomId < 1)
                {
                    if (towerId < 1)
                    {
                        return CreateFailureResponse("Choose the site and enter the room.");
                    }
                    return CreateFailureResponse("Could not save that room on the selected site.");
                }

                EnsureDepartmentRoomLink(departmentId, resolvedRoomId);

                // Access windows are blackout periods: no requests while one is active for this work area.
                string? blackout = GetBlackoutMessage(GetRoomWorkAreaId(resolvedRoomId, towerId), DateTime.Now);
                if (blackout != null)
                {
                    return CreateFailureResponse(blackout);
                }

                if (!ManagerIsAllowed(approverUserId, resolvedRoomId, departmentId))
                {
                    return CreateFailureResponse("That manager is not assigned to that location.");
                }

                int dr = _db.InsertAccessRequest(
                    phoneNumber,
                    workerId,
                    resolvedRoomId,
                    reason,
                    workType,
                    description,
                    supervisorName,
                    workFrom,
                    workTo);
                _db.InsertAccessRequestApproval(dr, approverUserId);

                DataRow worker = _db.GetWorkers(workerId, null).Rows[0];
                string workerFirstName = worker.Field<string>("FirstName")!;
                string workerLastName = worker.Field<string>("LastName")!;
                
                DataRow approver = _db.GetUsers(approverUserId).Rows[0];
                string approverFirstName = approver.Field<string>("FirstName")!;
                string approverLastName = approver.Field<string>("LastName")!;
                _db.InsertAuditEvent(
                    dr,
                    workerId,
                    approverUserId,
                    "AccessRequested",
                    $"Worker {workerFirstName} {workerLastName} submitted access request #{dr} for manager {approverFirstName} {approverLastName}.");

                DataTable dt = _db.GetWorkers(workerId,null);
                DataTable dt2 = _db.GetRooms(resolvedRoomId);
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
                    roomname = dt2.Rows[0].Field<string>("RoomName") ?? dt2.Rows[0].Field<string>("RoomNumber") ?? "";
                }

                string managerPhone = "";
                DataTable managers = _db.GetUsers(approverUserId);
                if (managers.Rows.Count > 0 && managers.Columns.Contains("PhoneNumber"))
                {
                    managerPhone = managers.Rows[0].Field<string>("PhoneNumber") ?? "";
                }

                try
                {
                    if (!string.IsNullOrWhiteSpace(managerPhone) && _twilio)
                    {
                        TwilioHelper twilio = new();
                      await twilio.ApprovalRequestAsync(firstname, lastname, sitename, workareaname, roomname, managerPhone);
                    }
                }
                catch (Exception smsEx)
                {
                    _db.LogError(
                        MethodBase.GetCurrentMethod()?.Module + "/" +
                        MethodBase.GetCurrentMethod()?.Name!,
                        "Twilio SMS skipped: " + smsEx.Message,
                        0);
                }
                return CreateSuccessResponseWithData(new { Id = dr, RoomId = resolvedRoomId });
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

        private int ResolveWorkAreaId(int workAreaId, int siteId)
        {
            DataTable all = _db.GetWorkAreas();
            if (workAreaId > 0)
            {
                foreach (DataRow row in all.Rows)
                {
                    if (Convert.ToInt32(row["Id"]) == workAreaId)
                    {
                        return workAreaId;
                    }
                }
                DataTable asSite = _db.GetWorkAreas(workAreaId);
                if (asSite.Rows.Count == 1)
                {
                    return Convert.ToInt32(asSite.Rows[0]["Id"]);
                }
            }
            int site = siteId > 0 ? siteId : 0;
            if (site > 0)
            {
                DataTable onSite = _db.GetWorkAreas(site);
                if (onSite.Rows.Count == 1)
                {
                    return Convert.ToInt32(onSite.Rows[0]["Id"]);
                }
            }
            return 0;
        }

        private int ResolveRoomId(int roomId, int workAreaId, string? roomText, string? locationKind)
        {
            if (roomId > 0)
            {
                DataTable existing = _db.GetRooms(roomId);
                if (existing.Rows.Count > 0)
                {
                    return roomId;
                }
            }

            string typed = (roomText ?? "").Trim();
            if (workAreaId < 1 || typed.Length == 0)
            {
                return 0;
            }

            DataTable rooms = _db.GetRooms(null, workAreaId);
            int matched = MatchRoomId(rooms, typed);
            if (matched > 0)
            {
                return matched;
            }

            var parsed = ParseEnteredRoom(typed, locationKind);
            try
            {
                int created = _db.InsertRoom(
                    workAreaId,
                    parsed.Number,
                    parsed.Name,
                    "Created from worker access request",
                    parsed.Kind);
                if (created > 0) return created;
            }
            catch
            {
                DataTable again = _db.GetRooms(null, workAreaId);
                int existing = MatchRoomId(again, typed);
                if (existing > 0) return existing;
            }

            DataTable after = _db.GetRooms(null, workAreaId);
            return MatchRoomId(after, typed);
        }

        private static int MatchRoomId(DataTable rooms, string query)
        {
            string q = Norm(query);
            if (q.Length == 0) return 0;
            var parsed = ParseEnteredRoom(query, null);
            string number = Norm(parsed.Number);
            string parsedName = Norm(parsed.Name);

            foreach (DataRow row in rooms.Rows)
            {
                string roomNumber = Norm(Col(row, "RoomNumber"));
                string name = Norm(Col(row, "RoomName", "Name"));
                string kind = Col(row, "LocationKind");
                string label = Norm(
                    name.Length > 0
                        ? name
                        : string.Equals(kind, "Riser", StringComparison.OrdinalIgnoreCase)
                            ? "riser " + roomNumber
                            : "apartment " + roomNumber);
                if (roomNumber == q || name == q || label == q || roomNumber == number || name == parsedName)
                {
                    return Convert.ToInt32(row["Id"]);
                }
            }
            return 0;
        }

        private static (string Number, string Name, string Kind) ParseEnteredRoom(string text, string? locationKind)
        {
            string q = text.Trim();
            string kind = string.IsNullOrWhiteSpace(locationKind) ? InferLocationKind(q) : locationKind.Trim();
            string number = q;
            if (q.StartsWith("riser ", StringComparison.OrdinalIgnoreCase))
            {
                kind = "Riser";
                number = q.Substring(6).Trim();
            }
            else if (q.StartsWith("apartment ", StringComparison.OrdinalIgnoreCase))
            {
                kind = "Apartment";
                number = q.Substring(10).Trim();
            }
            else if (q.StartsWith("apt ", StringComparison.OrdinalIgnoreCase))
            {
                kind = "Apartment";
                number = q.Substring(4).Trim();
            }
            if (number.Length == 0) number = q;
            if (!string.Equals(kind, "Riser", StringComparison.OrdinalIgnoreCase))
            {
                kind = "Apartment";
            }
            string name = string.Equals(kind, "Riser", StringComparison.OrdinalIgnoreCase)
                ? "Riser " + number
                : "Apartment " + number;
            return (number, name, kind);
        }

        private static string InferLocationKind(string text)
        {
            if (text.IndexOf("riser", StringComparison.OrdinalIgnoreCase) >= 0) return "Riser";
            if (text.StartsWith("apartment", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("apt ", StringComparison.OrdinalIgnoreCase))
            {
                return "Apartment";
            }
            if (text.Length > 0 && char.IsLetter(text[0]))
            {
                foreach (char c in text)
                {
                    if (char.IsDigit(c)) return "Riser";
                }
            }
            return "Apartment";
        }

        private static string Norm(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            return string.Join(" ", value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string Col(DataRow row, params string[] names)
        {
            foreach (string name in names)
            {
                if (row.Table.Columns.Contains(name) && row[name] != DBNull.Value)
                {
                    return Convert.ToString(row[name]) ?? "";
                }
            }
            return "";
        }

        private void EnsureDepartmentRoomLink(int departmentId, int roomId)
        {
            if (departmentId < 1 || roomId < 1) return;
            try
            {
                DataTable existing = _db.GetDepartmentRoomMappings();
                foreach (DataRow row in existing.Rows)
                {
                    int mappedDepartment = 0;
                    int mappedRoom = 0;
                    if (row.Table.Columns.Contains("DepartmentId") && row["DepartmentId"] != DBNull.Value)
                    {
                        mappedDepartment = Convert.ToInt32(row["DepartmentId"]);
                    }
                    if (row.Table.Columns.Contains("RoomId") && row["RoomId"] != DBNull.Value)
                    {
                        mappedRoom = Convert.ToInt32(row["RoomId"]);
                    }
                    if (mappedDepartment == departmentId && mappedRoom == roomId)
                    {
                        return;
                    }
                }
                _db.InsertDepartmentRoomMapping(departmentId, roomId);
            }
            catch
            {
            }
        }

        private bool ManagerIsAllowed(int approverUserId, int roomId, int departmentId)
        {
            try
            {
                if (_db.ManagerCanApprove(approverUserId, roomId, null, departmentId))
                {
                    return true;
                }
            }
            catch
            {
            }
            try
            {
                DataTable dt = _db.GetManagersForRoom(roomId, departmentId);
                foreach (DataRow row in dt.Rows)
                {
                    string raw = Col(row, "UserId", "Id");
                    if (int.TryParse(raw, out int id) && id == approverUserId)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return departmentId > 0;
            }
            return false;
        }

        public async Task<GenericResponse> UpdateAccessRequest(
            int userId,
            int id,
            int workerId,
            int roomId,
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
            try
            {
                // Workers (no staff token) cannot clock in or out once the approved
                // time frame has ended; the visit is overdue and a manager must close it.
                if (userId < 1 && (clockedInAt.HasValue || clockedOutAt.HasValue || completedAt.HasValue))
                {
                    DateTime? windowEnd = GetRequestWindowEnd(id);
                    if (windowEnd.HasValue && DateTime.Now > windowEnd.Value)
                    {
                        return CreateFailureResponse(
                            $"Your approved time frame ended at {windowEnd.Value:HH:mm} on {windowEnd.Value:d MMM}. " +
                            "This visit is overdue. Please contact your site manager to be signed out.");
                    }
                }

                if (clockedInAt.HasValue && !expectedClockOutAt.HasValue)
                {
                    expectedClockOutAt = ResolveExpectedClockOut(roomId, id);
                }

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

                // The worker (or a manager, for an overdue visit) has signed out of
                // the room: hand it to the Keyman to check it was left as found.
                if (id > 0 && (clockedOutAt.HasValue || completedAt.HasValue))
                {
                    await OpenRoomLeaveReview(id);
                }

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
        public GenericResponse GetAccessRequestApprovals(
            int userId,
            int id,
            int accessRequestId,
            int approverUserId,
            string? status,
            string? search,
            int skip,
            int take)
        {
            try
            {
                DataTable dt = _db.GetAccessRequestApprovals(
                    id,
                    accessRequestId,
                    approverUserId,
                    status,
                    search,
                    skip,
                    take);

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
                    return CreateFailureResponse("You are not assigned to this location.");
                }

                _db.UpdateAccessRequestApproval(
                    id,
                    normalized,
                    comment);

                if (accessRequestId > 0)
                {
                    DateTime? expectedOut = null;
                    if (approved)
                    {
                        expectedOut = _db.GetAccessRequestWorkTo(accessRequestId)
                            ?? ResolveExpectedClockOut(roomId, accessRequestId);
                    }
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
                        null,
                        expectedOut,
                        null,
                        null);
                }

                DataRow approver = _db.GetUsers(approverUserId).Rows[0];
                string approverFirstName = approver.Field<string>("FirstName")!;
                string approverLastName = approver.Field<string>("LastName")!;

                _db.InsertAuditEvent(
                    accessRequestId > 0 ? accessRequestId : null,
                    workerId,
                    approverUserId,
                    normalized,
                    $"Manager #{approverFirstName} {approverLastName} marked request #{accessRequestId} as {normalized}.");

                DataRow dr = _db.GetRoomByAccessRequestId(accessRequestId);
                if (dr != null)
                {
                    string phone = dr.Table.Columns.Contains("PhoneNumber") ? dr.Field<string>("PhoneNumber") ?? "" : "";
                    string roomName = dr.Table.Columns.Contains("RoomNumber") ? dr.Field<string>("RoomNumber") ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(phone) && _twilio)
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

        public async Task<GenericResponse> InsertAccessPhoto(
            int userId,
            int accessRequestId,
            string photoType,
            int uploadedByWorkerId,
            IFormFile file)
        {
            try
            {
                string publicBaseUrl = _config["PublicApiBaseUrl"]!;

                if (file == null || file.Length == 0)
                    return CreateFailureResponse("No file");
                if (file.Length > 10_000_000)
                    return CreateFailureResponse("File too large");
                if (string.IsNullOrEmpty(file.ContentType) || !file.ContentType.StartsWith("image/"))
                    return CreateFailureResponse("Images only");

                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".jpg";
                var safeName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";

                var webRoot = _env.WebRootPath;
                var dir = Path.Combine(webRoot, "uploads", accessRequestId.ToString());
                Directory.CreateDirectory(dir);
                var physicalPath = Path.Combine(dir, safeName);

                await using (var stream = System.IO.File.Create(physicalPath))
                    await file.CopyToAsync(stream);

                var baseUrl = (publicBaseUrl ?? "").TrimEnd('/');
                if (string.IsNullOrEmpty(baseUrl))
                    return CreateFailureResponse("PublicApiBaseUrl is not configured");

                var storagePath = $"{baseUrl}/uploads/{accessRequestId}/{safeName}";

                _db.InsertAccessPhoto(
                    accessRequestId,
                    storagePath,
                    file.FileName,
                    file.ContentType,
                    (int)file.Length,
                    uploadedByWorkerId,
                    DateTime.UtcNow,
                    string.IsNullOrWhiteSpace(photoType) ? "ClockOut" : photoType);

                return CreateSuccessResponseWithData(storagePath);
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
                if (_twilio)
                {
                    await twilio.SendMessageAsync(phoneNumber, code);
                }
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

        public GenericResponse GetDepartments(int userId, int siteId = 0)
        {
            try
            {
                DataTable dt = _db.GetDepartments(siteId);

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

        public GenericResponse GetRoomManagers(int userId)
        {
            try
            {
                DataTable dt = _db.GetRoomManagers();
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

        public GenericResponse InsertRoomManager(int userId, int roomId, int managerUserId, bool isPrimary)
        {
            try
            {
                _db.InsertRoomManager(roomId, managerUserId, isPrimary);
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

        public GenericResponse UpdateRoomManager(int userId, int id, int roomId, int managerUserId, bool isPrimary)
        {
            try
            {
                _db.UpdateRoomManager(id, roomId, managerUserId, isPrimary);
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

        // ---------------------------------------------------------------
        // Keyman: room-leave reviews
        // ---------------------------------------------------------------

        public const string KEYMAN_ROLE = "Keyman";

        /// <summary>Roles with manager-level access to the console. Keyman is one of them.</summary>
        public static bool IsManagerRole(string? role)
        {
            string r = (role ?? "").Trim();
            return r.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                || r.Equals("SiteManager", StringComparison.OrdinalIgnoreCase)
                || r.Equals("Manager", StringComparison.OrdinalIgnoreCase)
                || r.Equals(KEYMAN_ROLE, StringComparison.OrdinalIgnoreCase);
        }

        private string UserRole(int userId)
        {
            if (userId < 1) return "";
            DataTable dt = _db.GetUsers(userId);
            return dt.Rows.Count > 0 ? Col(dt.Rows[0], "Role") : "";
        }

        /// <summary>Only a Keyman (or an Admin standing in) signs off a room.</summary>
        private bool CanReviewRoomLeave(int userId)
        {
            string role = UserRole(userId);
            return role.Equals(KEYMAN_ROLE, StringComparison.OrdinalIgnoreCase)
                || role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Keymen to ping for a room: those mapped to it the same way a manager is
        /// (department or tower); if none are mapped, every active Keyman.
        /// </summary>
        private List<DataRow> KeymenForRoom(int roomId)
        {
            List<DataRow> keymen = new();
            foreach (DataRow row in _db.GetUsers().Rows)
            {
                bool active = !row.Table.Columns.Contains("IsActive") || row["IsActive"] is not bool flag || flag;
                if (active && Col(row, "Role").Equals(KEYMAN_ROLE, StringComparison.OrdinalIgnoreCase))
                {
                    keymen.Add(row);
                }
            }
            if (roomId < 1) return keymen;

            List<DataRow> mapped = keymen
                .Where(k => int.TryParse(Col(k, "Id"), out int id) && _db.ManagerCanApprove(id, roomId, null))
                .ToList();
            return mapped.Count > 0 ? mapped : keymen;
        }

        /// <summary>
        /// Called once the visit is signed out. Creates the pending review and pings
        /// the Keyman (in-app, plus SMS when Twilio is on). Never fails the sign-out.
        /// </summary>
        private async Task OpenRoomLeaveReview(int accessRequestId)
        {
            try
            {
                DataRow review = _db.InsertRoomLeaveReview(accessRequestId);
                if (review == null || !Convert.ToBoolean(review["Created"]))
                {
                    return; // already open or decided: ping only once
                }

                DataRow? visit = _db.GetRoomLeaveReviews(0, accessRequestId).Rows.Cast<DataRow>().FirstOrDefault();
                string worker = visit == null ? "A worker" : $"{Col(visit, "WorkerFirstName")} {Col(visit, "WorkerLastName")}".Trim();
                string room = visit == null ? $"request #{accessRequestId}" : RoomText(visit);
                int roomId = visit != null && int.TryParse(Col(visit, "RoomId"), out int rid) ? rid : 0;
                int? workerId = visit != null && int.TryParse(Col(visit, "WorkerId"), out int wid) ? wid : null;

                string message = $"{worker} has left {room}. Check the room and approve or reject the room leave.";

                _db.InsertAuditEvent(
                    accessRequestId,
                    workerId,
                    null,
                    "RoomLeaveRequested",
                    $"{worker} signed out of {room}; waiting for the Keyman to check the room.");

                foreach (DataRow keyman in KeymenForRoom(roomId))
                {
                    if (!int.TryParse(Col(keyman, "Id"), out int keymanId)) continue;
                    _db.InsertNotification(keymanId, null, accessRequestId, "RoomLeaveReview", "IN_APP", message);

                    string phone = Col(keyman, "PhoneNumber");
                    if (_twilio && !string.IsNullOrWhiteSpace(phone))
                    {
                        try
                        {
                            await new TwilioHelper().SendPlainMessageAsync(phone, message + " Open SiteGate > Keyman.");
                        }
                        catch (Exception smsEx)
                        {
                            _db.LogError("AccessProcessor/OpenRoomLeaveReview", "Twilio SMS skipped: " + smsEx.Message, keymanId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _db.LogError("AccessProcessor/OpenRoomLeaveReview", ex.Message + " " + ex.StackTrace, 0);
            }
        }

        private static string RoomText(DataRow row)
        {
            string number = Col(row, "RoomNumber");
            string name = Col(row, "RoomName");
            string label = string.IsNullOrWhiteSpace(name) || name == number ? number : $"{number} {name}".Trim();
            if (string.IsNullOrWhiteSpace(label)) label = "the room";
            string area = Col(row, "WorkAreaName");
            string site = Col(row, "SiteName");
            string where = string.Join(", ", new[] { area, site }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return string.IsNullOrWhiteSpace(where) ? label : $"{label} ({where})";
        }

        public GenericResponse GetRoomLeaveReviews(int userId, int id, int accessRequestId, string? status)
        {
            try
            {
                string? normalized = string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : NormalizeReviewStatus(status);
                return CreateSuccessResponseWithData(_db.GetRoomLeaveReviews(id, accessRequestId, normalized));
            }
            catch (Exception ex)
            {
                _db.LogError("AccessProcessor/GetRoomLeaveReviews", ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        private static string NormalizeReviewStatus(string status) =>
            status.Trim().ToLowerInvariant() switch
            {
                "approved" or "approve" => "Approved",
                "rejected" or "reject" => "Rejected",
                _ => "Pending",
            };

        public async Task<GenericResponse> ReviewRoomLeave(int userId, int id, string status, string? reason)
        {
            try
            {
                if (!CanReviewRoomLeave(userId))
                {
                    return CreateFailureResponse("Only a Keyman can sign off a room.");
                }

                string decision = NormalizeReviewStatus(status ?? "");
                if (decision == "Pending")
                {
                    return CreateFailureResponse("Choose approve or reject.");
                }
                reason = reason?.Trim();
                if (decision == "Rejected" && string.IsNullOrWhiteSpace(reason))
                {
                    return CreateFailureResponse("Give a reason for rejecting the room leave.");
                }

                DataRow? review = _db.GetRoomLeaveReviews(id).Rows.Cast<DataRow>().FirstOrDefault();
                if (review == null)
                {
                    return CreateFailureResponse("That room check no longer exists.");
                }

                if (!_db.UpdateRoomLeaveReview(id, decision, userId, string.IsNullOrWhiteSpace(reason) ? null : reason))
                {
                    return CreateFailureResponse("This room has already been checked.");
                }

                int accessRequestId = Convert.ToInt32(review["AccessRequestId"]);
                int? workerId = int.TryParse(Col(review, "WorkerId"), out int wid) ? wid : null;
                string room = RoomText(review);
                string worker = $"{Col(review, "WorkerFirstName")} {Col(review, "WorkerLastName")}".Trim();

                DataTable reviewer = _db.GetUsers(userId);
                string keymanName = reviewer.Rows.Count > 0
                    ? $"{Col(reviewer.Rows[0], "FirstName")} {Col(reviewer.Rows[0], "LastName")}".Trim()
                    : $"User #{userId}";

                _db.InsertAuditEvent(
                    accessRequestId,
                    workerId,
                    userId,
                    decision == "Approved" ? "RoomLeaveApproved" : "RoomLeaveRejected",
                    decision == "Approved"
                        ? $"Keyman {keymanName} checked {room} and approved the room leave."
                        : $"Keyman {keymanName} rejected the room leave for {room}. Reason: {reason}");

                if (decision == "Rejected")
                {
                    // Tell the managers who approved the visit, and the worker.
                    string managerMessage = $"Keyman {keymanName} rejected the room leave for {room} ({worker}). Reason: {reason}";
                    foreach (DataRow approval in _db.GetAccessRequestApprovals(0, accessRequestId, 0, null, null, 0, 0).Rows)
                    {
                        if (int.TryParse(Col(approval, "ApproverUserId"), out int approverId) && approverId > 0)
                        {
                            _db.InsertNotification(approverId, null, accessRequestId, "RoomLeaveRejected", "IN_APP", managerMessage);
                        }
                    }

                    string phone = Col(review, "WorkerPhoneNumber");
                    if (_twilio && !string.IsNullOrWhiteSpace(phone))
                    {
                        try
                        {
                            await new TwilioHelper().SendPlainMessageAsync(
                                phone,
                                $"The room check for {room} was not signed off. Reason: {reason}. Please contact the site team.");
                        }
                        catch (Exception smsEx)
                        {
                            _db.LogError("AccessProcessor/ReviewRoomLeave", "Twilio SMS skipped: " + smsEx.Message, userId);
                        }
                    }
                }

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError("AccessProcessor/ReviewRoomLeave", ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        DateTime? GetRequestWindowEnd(int accessRequestId)
        {
            if (accessRequestId < 1) return null;
            DataTable dt = _db.GetAccessRequests(accessRequestId, 0, 0, null, null, 0, 1);
            if (dt.Rows.Count == 0) return null;
            DataRow row = dt.Rows[0];
            foreach (string col in new[] { "WorkTo", "ExpectedClockOutAt" })
            {
                if (dt.Columns.Contains(col) && row[col] != DBNull.Value)
                {
                    return Convert.ToDateTime(row[col]);
                }
            }
            return null;
        }

        DateTime ResolveExpectedClockOut(int? roomId, int accessRequestId)
        {
            DateTime now = DateTime.Now;
            DateTime expected = now.AddHours(8);
            try
            {
                int workAreaId = 0;
                if (roomId.HasValue && roomId.Value > 0)
                {
                    workAreaId = GetRoomWorkAreaId(roomId.Value, 0);
                }
                if (workAreaId < 1 && accessRequestId > 0)
                {
                    DataRow room = _db.GetRoomByAccessRequestId(accessRequestId);
                    if (room != null && room.Table.Columns.Contains("WorkAreaId") && room["WorkAreaId"] != DBNull.Value)
                    {
                        workAreaId = Convert.ToInt32(room["WorkAreaId"]);
                    }
                }
                // Workers must be out before the next blackout window starts today.
                foreach (DataRow row in ActiveWindows(workAreaId, now.DayOfWeek))
                {
                    TimeSpan start = ToTimeSpan(row["StartTime"]);
                    DateTime startAt = now.Date.Add(start);
                    if (startAt > now && startAt < expected)
                    {
                        expected = startAt;
                    }
                }
            }
            catch
            {
            }
            return expected;
        }

        int GetRoomWorkAreaId(int roomId, int fallbackWorkAreaId)
        {
            DataTable rooms = _db.GetRooms(roomId);
            if (rooms.Rows.Count > 0 && rooms.Columns.Contains("WorkAreaId") && rooms.Rows[0]["WorkAreaId"] != DBNull.Value)
            {
                return Convert.ToInt32(rooms.Rows[0]["WorkAreaId"]);
            }
            return fallbackWorkAreaId;
        }

        List<DataRow> ActiveWindows(int workAreaId, DayOfWeek day)
        {
            List<DataRow> result = new();
            if (workAreaId < 1) return result;
            DataTable windows = _db.GetAccessWindows();
            foreach (DataRow row in windows.Rows)
            {
                if (Convert.ToInt32(row["WorkAreaId"]) != workAreaId) continue;
                if (Convert.ToInt32(row["DayOfWeek"]) != (int)day) continue;
                if (windows.Columns.Contains("IsActive") && row["IsActive"] != DBNull.Value && !Convert.ToBoolean(row["IsActive"])) continue;
                result.Add(row);
            }
            return result;
        }

        // Returns a message when the work area has an active blackout window covering `when`, otherwise null.
        string? GetBlackoutMessage(int workAreaId, DateTime when)
        {
            TimeSpan now = when.TimeOfDay;
            foreach (DataRow row in ActiveWindows(workAreaId, when.DayOfWeek))
            {
                TimeSpan start = ToTimeSpan(row["StartTime"]);
                TimeSpan end = ToTimeSpan(row["EndTime"]);
                bool inside = start <= end
                    ? now >= start && now < end
                    : now >= start || now < end; // window that runs past midnight
                if (inside)
                {
                    return $"Access requests are closed for this area on {when.DayOfWeek}s between {start:hh\\:mm} and {end:hh\\:mm}. Please try again after {end:hh\\:mm}.";
                }
            }
            return null;
        }

        static TimeSpan ToTimeSpan(object value)
        {
            return value is TimeSpan ts ? ts : TimeSpan.Parse(value.ToString()!);
        }
    }
}