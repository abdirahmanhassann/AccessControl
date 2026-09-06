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

        public GenericResponse SignUp(string email, string firstName, string lastName, string password, string phoneNumber, string role = "Manager")
        {
            try
            {
                // ###TODO check for profanity in displayName or Shop
                Encryptor enc = new(_config);
                string salt = enc.GenerateRandomSalt();
                string pwdEncrypted = enc.Encrypt(password, salt);

                _db.Signup(email, firstName, lastName, pwdEncrypted, salt, phoneNumber, role);
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
                            await twilio.SendPlainMessageAsync(
                                phone,
                                $"SiteGate password reset code: {code:D6}. It expires in 10 minutes.");
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


        // ============================================================
        // WORK AREAS
        // ============================================================

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
            string role,
            string? password = null)
        {
            try
            {
                int createdId = _db.InsertUser(
                    firstName,
                    lastName,
                    email,
                    phoneNumber,
                    role);
                if (!string.IsNullOrWhiteSpace(password) && createdId > 0)
                {
                    Encryptor enc = new(_config);
                    string salt = enc.GenerateRandomSalt();
                    _db.SetUserPassword(createdId, enc.Encrypt(password, salt), salt);
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
                _db.InsertAuditEvent(
                    dr,
                    workerId,
                    approverUserId,
                    "AccessRequested",
                    $"Worker #{workerId} submitted access request #{dr} for manager #{approverUserId}.");

                DataTable dt = _db.GetWorkers(workerId);
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
                    if (!string.IsNullOrWhiteSpace(managerPhone))
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
                // Unique constraint if two workers create the same link together.
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
                /* Missing RoomManagers or older procedure — fall through to department list. */
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

        public GenericResponse UpdateAccessRequest(
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

        DateTime ResolveExpectedClockOut(int? roomId, int accessRequestId)
        {
            int workAreaId = 0;
            try
            {
                if (roomId.HasValue && roomId.Value > 0)
                {
                    DataTable rooms = _db.GetRooms(roomId.Value);
                    if (rooms.Rows.Count > 0 && rooms.Columns.Contains("WorkAreaId") && rooms.Rows[0]["WorkAreaId"] != DBNull.Value)
                    {
                        workAreaId = Convert.ToInt32(rooms.Rows[0]["WorkAreaId"]);
                    }
                }
                if (workAreaId < 1 && accessRequestId > 0)
                {
                    DataRow room = _db.GetRoomByAccessRequestId(accessRequestId);
                    if (room != null && room.Table.Columns.Contains("WorkAreaId") && room["WorkAreaId"] != DBNull.Value)
                    {
                        workAreaId = Convert.ToInt32(room["WorkAreaId"]);
                    }
                }
                int dayOfWeek = (int)DateTime.Now.DayOfWeek;
                DataTable windows = _db.GetAccessWindows();
                foreach (DataRow row in windows.Rows)
                {
                    if (workAreaId > 0 && Convert.ToInt32(row["WorkAreaId"]) != workAreaId) continue;
                    if (Convert.ToInt32(row["DayOfWeek"]) != dayOfWeek) continue;
                    TimeSpan end = row["EndTime"] is TimeSpan ts ? ts : TimeSpan.Parse(row["EndTime"].ToString()!);
                    return DateTime.Today.Add(end);
                }
            }
            catch
            {
                /* fall through to default window */
            }
            return DateTime.Now.AddHours(8);
        }
    }
}