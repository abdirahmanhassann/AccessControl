using AccessControl.Processor;
using AccessControl.Security;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using static AccessControl.Helpers.ApiHelper;
using static AccessControl.Model.ApiRequests;
using static AccessControl.Model.ApiResponses;

namespace AccessControl.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AccessController : ControllerBase
    {
        private AccessProcessor _proc;
        private Encryptor _security;

        public AccessController(IConfiguration config)
        {
            _proc = new AccessProcessor(config);
            _security = new(config);
        }

        [HttpPost("login")]
        public string Login(LoginRequest req)
        {
            GenericResponse ret = _proc.Login(req.Email, req.Password);
            return JsonConvert.SerializeObject(ret, Newtonsoft.Json.Formatting.Indented);
        }

        [HttpPost("signup")]
        public string Signup(CreateUserRequest req)
        {
            GenericResponse ret = _proc.SignUp(req.Email, req.FirstName, req.LastName, req.Password, req.PhoneNumber);
            return JsonConvert.SerializeObject(ret, Newtonsoft.Json.Formatting.Indented);
        }

        [HttpPost("scanqr")]
        public string ScanQrCode(ScanQrCodeRequest req)
        {
            GenericResponse ret = _proc.ScanQrCode(
                req.QrCodeIdentifier);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        // ============================================================
        // SITES
        // ============================================================

        [HttpPost("getsites")]
        public string GetSites(TokenOnlyRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.GetSites((int)accountId);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("insertsite")]
        public string InsertSite(InsertSiteRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.InsertSite(
                (int)accountId,
                req.Name,
                req.Address,
                req.Reference);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("updatesite")]
        public string UpdateSite(UpdateSiteRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.UpdateSite(
                (int)accountId,
                req.Id,
                req.Name,
                req.Address,
                req.Reference,
                req.IsActive);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        // ============================================================
        // WORK AREAS
        // ============================================================

        [HttpPost("getworkareas")]
        public string GetWorkAreas(TokenOnlyRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.GetWorkAreas((int)accountId);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("insertworkarea")]
        public string InsertWorkArea(InsertWorkAreaRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.InsertWorkArea(
                (int)accountId,
                req.SiteId,
                req.Name,
                req.Description);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("updateworkarea")]
        public string UpdateWorkArea(UpdateWorkAreaRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.UpdateWorkArea(
                (int)accountId,
                req.Id,
                req.SiteId,
                req.Name,
                req.Description,
                req.IsActive);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        // ============================================================
        // USERS
        // ============================================================

        [HttpPost("getusers")]
        public string GetUsers(TokenOnlyRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.GetUsers((int)accountId);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("insertuser")]
        public string InsertUser(InsertUserRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.InsertUser(
                (int)accountId,
                req.FirstName,
                req.LastName,
                req.Email,
                req.PhoneNumber,
                req.Role);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("updateuser")]
        public string UpdateUser(UpdateUserRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.UpdateUser(
                (int)accountId,
                req.Id,
                req.FirstName,
                req.LastName,
                req.Email,
                req.PhoneNumber,
                req.Role,
                req.IsActive);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        // ============================================================
        // WORK AREA MANAGERS
        // ============================================================

        [HttpPost("getworkareamanagers")]
        public string GetWorkAreaManagers(TokenOnlyRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret =
                _proc.GetWorkAreaManagers((int)accountId);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("insertworkareamanager")]
        public string InsertWorkAreaManager(
            InsertWorkAreaManagerRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.InsertWorkAreaManager(
                (int)accountId,
                req.WorkAreaId,
                req.ManagerUserId,
                req.IsPrimary);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("updateworkareamanager")]
        public string UpdateWorkAreaManager(
            UpdateWorkAreaManagerRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.UpdateWorkAreaManager(
                (int)accountId,
                req.Id,
                req.WorkAreaId,
                req.ManagerUserId,
                req.IsPrimary);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        // ============================================================
        // WORKERS
        // ============================================================

        [HttpPost("getworkers")]
        public string GetWorkers(TokenOnlyRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.GetWorkers((int)accountId);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("insertworker")]
        public string InsertWorker(InsertWorkerRequest req)
        {
            GenericResponse ret = _proc.InsertWorker(
                req.FirstName,
                req.LastName,
                req.PhoneNumber,
                req.CompanyName);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("updateworker")]
        public string UpdateWorker(UpdateWorkerRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.UpdateWorker(
                (int)accountId,
                req.Id,
                req.FirstName,
                req.LastName,
                req.PhoneNumber,
                req.CompanyName,
                req.IsActive);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        // ============================================================
        // ROOMS
        // ============================================================

        [HttpPost("getrooms")]
        public string GetRooms(TokenOnlyRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.GetRooms((int)accountId);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("insertroom")]
        public string InsertRoom(InsertRoomRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.InsertRoom(
                (int)accountId,
                req.WorkAreaId,
                req.RoomNumber,
                req.Name,
                req.Description);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("updateroom")]
        public string UpdateRoom(UpdateRoomRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.UpdateRoom(
                (int)accountId,
                req.Id,
                req.WorkAreaId,
                req.RoomNumber,
                req.Name,
                req.QrCodeIdentifier,
                req.Description,
                req.IsActive);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        // ============================================================
        // ACCESS WINDOWS
        // ============================================================

        [HttpPost("getaccesswindows")]
        public string GetAccessWindows(TokenOnlyRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret =
                _proc.GetAccessWindows((int)accountId);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("insertaccesswindow")]
        public string InsertAccessWindow(
            InsertAccessWindowRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.InsertAccessWindow(
                (int)accountId,
                req.WorkAreaId,
                req.DayOfWeek,
                req.StartTime,
                req.EndTime);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("updateaccesswindow")]
        public string UpdateAccessWindow(
            UpdateAccessWindowRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.UpdateAccessWindow(
                (int)accountId,
                req.Id,
                req.WorkAreaId,
                req.DayOfWeek,
                req.StartTime,
                req.EndTime,
                req.IsActive);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        // ============================================================
        // ACCESS REQUESTS
        // ============================================================

        [HttpPost("getaccessrequests")]
        public string GetAccessRequests(TokenOnlyRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret =
                _proc.GetAccessRequests((int)accountId);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("insertaccessrequest")]
        public async Task<string> InsertAccessRequest(
            InsertAccessRequestRequest req)
        {
            GenericResponse ret = await _proc.InsertAccessRequest(
                req.PhoneNumber,
                req.workerId,
                req.RoomId,
                req.Reason,
                req.WorkType,
                req.Description);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("updateaccessrequest")]
        public string UpdateAccessRequest(
            UpdateAccessRequestRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.UpdateAccessRequest(
                (int)accountId,
                req.Id,
                req.WorkerId,
                req.RoomId,
                req.Status,
                req.Reason,
                req.WorkType,
                req.Description,
                req.ApprovedAt,
                req.RejectedAt,
                req.ClockedInAt,
                req.ExpectedClockOutAt,
                req.ClockedOutAt,
                req.CompletedAt);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        // ============================================================
        // ACCESS REQUEST APPROVALS
        // ============================================================

        [HttpPost("getaccessrequestapprovals")]
        public string GetAccessRequestApprovals(
            TokenOnlyRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret =
                _proc.GetAccessRequestApprovals((int)accountId);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("insertaccessrequestapproval")]
        public string InsertAccessRequestApproval(
            InsertAccessRequestApprovalRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret =
                _proc.InsertAccessRequestApproval(
                    (int)accountId,
                    req.AccessRequestId,
                    req.ApproverUserId
                    );

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("updateaccessrequestapproval")]
        public async Task<string> UpdateAccessRequestApproval(
            UpdateAccessRequestApprovalRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

           GenericResponse ret =
           await   _proc.UpdateAccessRequestApproval(
                    req.AccessRequestId,
                    (int)accountId,
                    req.Status,
                    req.Comment);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        // ============================================================
        // ACCESS PHOTOS
        // ============================================================

        [HttpPost("getaccessphotos")]
        public string GetAccessPhotos(TokenOnlyRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret =
                _proc.GetAccessPhotos((int)accountId);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("insertaccessphoto")]
        public string InsertAccessPhoto(
            InsertAccessPhotoRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.InsertAccessPhoto(
                (int)accountId,
                req.AccessRequestId,
                req.StoragePath,
                req.OriginalFileName,
                req.ContentType,
                req.FileSize,
                req.UploadedByWorkerId,
                req.CapturedAt,
                req.PhotoType);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("updateaccessphoto")]
        public string UpdateAccessPhoto(
            UpdateAccessPhotoRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.UpdateAccessPhoto(
                (int)accountId,
                req.Id,
                req.AccessRequestId,
                req.StoragePath,
                req.OriginalFileName,
                req.ContentType,
                req.FileSize,
                req.UploadedByWorkerId,
                req.CapturedAt,
                req.PhotoType);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        // ============================================================
        // OTP VERIFICATIONS
        // ============================================================

        [HttpPost("getotpverifications")]
        public string GetOTPVerifications(
            TokenOnlyRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret =
                _proc.GetOTPVerifications((int)accountId);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("insertotpverification")]
        public async Task<string> InsertOTPVerification(
          InsertOTPVerificationRequest req)
        {

            GenericResponse ret = await _proc.InsertOTPVerification(
                req.PhoneNumber,
                req.ExpiresAt);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }
        [HttpPost("verifyotp")]
        public string VerifyOTP(VerifyOTP req)
        {

            GenericResponse ret = _proc.VerifyOTP(req.code);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("updateotpverification")]
        public string UpdateOTPVerification(
            UpdateOTPVerificationRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret =
                _proc.UpdateOTPVerification(
                    (int)accountId,
                    req.Id,
                    req.WorkerId,
                    req.PhoneNumber,
                    req.CodeHash,
                    req.ExpiresAt,
                    req.VerifiedAt,
                    req.AttemptCount);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        // ============================================================
        // NOTIFICATIONS
        // ============================================================

        [HttpPost("getnotifications")]
        public string GetNotifications(
            TokenOnlyRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret =
                _proc.GetNotifications((int)accountId);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("insertnotification")]
        public string InsertNotification(
            InsertNotificationRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.InsertNotification(
                (int)accountId,
                req.UserId,
                req.WorkerId,
                req.AccessRequestId,
                req.Type,
                req.Channel,
                req.Message);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("updatenotification")]
        public string UpdateNotification(
            UpdateNotificationRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.UpdateNotification(
                (int)accountId,
                req.Id,
                req.UserId,
                req.WorkerId,
                req.AccessRequestId,
                req.Type,
                req.Channel,
                req.Message,
                req.Status,
                req.SentAt,
                req.ReadAt);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        // ============================================================
        // AUDIT EVENTS
        // ============================================================

        [HttpPost("getauditevents")]
        public string GetAuditEvents(
            TokenOnlyRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret =
                _proc.GetAuditEvents((int)accountId);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("insertauditevent")]
        public string InsertAuditEvent(
            InsertAuditEventRequest req)
        {
            int accountId = _security.CheckToken(req.Token);

            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(
                    new GenericResponse()
                    {
                        Result = false,
                        ErrorMessage = TOKEN_ERROR
                    });
            }

            GenericResponse ret = _proc.InsertAuditEvent(
                (int)accountId,
                req.AccessRequestId,
                req.WorkerId,
                req.UserId,
                req.EventType,
                req.Description,
                req.IpAddress,
                req.Metadata);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }
    }
}