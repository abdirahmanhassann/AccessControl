using AccessControl.Processor;
using AccessControl.Security;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Twilio.Http;
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
            int accountId = 0;
            if (!string.IsNullOrWhiteSpace(req.Token))
            {
                accountId = _security.CheckToken(req.Token);
            }

            GenericResponse ret = _proc.GetUsers(accountId);

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
        public string GetAccessRequests(GetAccessRequestsRequest req)
        {
            int accountId = _security.CheckToken(req.Token ?? "");

            if (accountId < 1)
            {
                if (req.WorkerId < 1 && req.Id < 1)
                {
                    return JsonConvert.SerializeObject(
                        new GenericResponse()
                        {
                            Result = false,
                            ErrorMessage = TOKEN_ERROR
                        });
                }
                accountId = 0;
            }

            GenericResponse ret =
                _proc.GetAccessRequests(
                    (int)accountId,
                    req.Id,
                    req.WorkerId,
                    req.RoomId,
                    req.Status,
                    req.Search,
                    req.Skip,
                    req.Take);

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
                req.WorkerId,
                req.RoomId,
                req.Reason,
                req.WorkType,
                req.Description,
                req.ApproverUserId);

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }


        [HttpPost("updateaccessrequest")]
        public string UpdateAccessRequest(
            UpdateAccessRequestRequest req)
        {
            int accountId = _security.CheckToken(req.Token ?? "");

            if (accountId < 1)
            {
                if (req.Id < 1 || req.WorkerId < 1)
                {
                    return JsonConvert.SerializeObject(
                        new GenericResponse()
                        {
                            Result = false,
                            ErrorMessage = TOKEN_ERROR
                        });
                }
                accountId = 0;
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

        [HttpPost("getaccessrequestapprovals")]
        public string GetAccessRequestApprovals(
            GetAccessRequestApprovalsRequest req)
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
                _proc.GetAccessRequestApprovals(
                    (int)accountId,
                    req.Id,
                    req.AccessRequestId,
                    req.ApproverUserId,
                    req.Status,
                    req.Search,
                    req.Skip,
                    req.Take);

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
                return JsonConvert.SerializeObject(new GenericResponse
                {
                    Result = false,
                    ErrorMessage = TOKEN_ERROR
                });
            }

            int approvalId = req.Id != 0 ? req.Id : req.AccessRequestApprovalId;

            GenericResponse ret = await _proc.UpdateAccessRequestApproval(
                approvalId,
                req.AccessRequestId,
                req.ApproverUserId != 0 ? req.ApproverUserId : accountId,
                req.WorkerId,
                req.RoomId,
                req.Status,
                req.Comment,
                req.Reason,
                req.WorkType,
                req.Description,
                req.ApprovedAt,
                req.RejectedAt);

            return JsonConvert.SerializeObject(ret, Newtonsoft.Json.Formatting.Indented);
        }

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
                req.Description
                );

            return JsonConvert.SerializeObject(
                ret,
                Newtonsoft.Json.Formatting.Indented);
        }

        private string? Guard(string token, out int accountId)
        {
            accountId = string.IsNullOrWhiteSpace(token) ? 0 : _security.CheckToken(token);
            if (accountId < 1)
            {
                return JsonConvert.SerializeObject(new GenericResponse
                {
                    Result = false,
                    ErrorMessage = TOKEN_ERROR
                });
            }
            return null;
        }

        [HttpPost("getdepartments")]
        public string GetDepartments(TokenOnlyRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return JsonConvert.SerializeObject(_proc.GetDepartments(accountId), Newtonsoft.Json.Formatting.Indented);
        }

        [HttpPost("insertdepartment")]
        public string InsertDepartment(InsertDepartmentRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return JsonConvert.SerializeObject(_proc.InsertDepartment(accountId, req.SiteId, req.Name, req.Description), Newtonsoft.Json.Formatting.Indented);
        }

        [HttpPost("updatedepartment")]
        public string UpdateDepartment(UpdateDepartmentRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return JsonConvert.SerializeObject(_proc.UpdateDepartment(accountId, req.Id, req.SiteId, req.Name, req.Description, req.IsActive), Newtonsoft.Json.Formatting.Indented);
        }

        [HttpPost("getdepartmentroommappings")]
        public string GetDepartmentRoomMappings(TokenOnlyRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return JsonConvert.SerializeObject(_proc.GetDepartmentRoomMappings(accountId), Newtonsoft.Json.Formatting.Indented);
        }

        [HttpPost("insertdepartmentroommapping")]
        public string InsertDepartmentRoomMapping(InsertDepartmentRoomMappingRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return JsonConvert.SerializeObject(_proc.InsertDepartmentRoomMapping(accountId, req.DepartmentId, req.RoomId), Newtonsoft.Json.Formatting.Indented);
        }

        [HttpPost("deletedepartmentroommapping")]
        public string DeleteDepartmentRoomMapping(IdOnlyRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return JsonConvert.SerializeObject(_proc.DeleteDepartmentRoomMapping(accountId, req.Id), Newtonsoft.Json.Formatting.Indented);
        }

        [HttpPost("getdepartmentmanagermappings")]
        public string GetDepartmentManagerMappings(TokenOnlyRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return JsonConvert.SerializeObject(_proc.GetDepartmentManagerMappings(accountId), Newtonsoft.Json.Formatting.Indented);
        }

        [HttpPost("insertdepartmentmanagermapping")]
        public string InsertDepartmentManagerMapping(InsertDepartmentManagerMappingRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return JsonConvert.SerializeObject(_proc.InsertDepartmentManagerMapping(accountId, req.DepartmentId, req.UserId, req.IsPrimary), Newtonsoft.Json.Formatting.Indented);
        }

        [HttpPost("updatedepartmentmanagermapping")]
        public string UpdateDepartmentManagerMapping(UpdateDepartmentManagerMappingRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return JsonConvert.SerializeObject(_proc.UpdateDepartmentManagerMapping(accountId, req.Id, req.DepartmentId, req.UserId, req.IsPrimary), Newtonsoft.Json.Formatting.Indented);
        }

        [HttpPost("deletedepartmentmanagermapping")]
        public string DeleteDepartmentManagerMapping(IdOnlyRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return JsonConvert.SerializeObject(_proc.DeleteDepartmentManagerMapping(accountId, req.Id), Newtonsoft.Json.Formatting.Indented);
        }

        [HttpPost("getworkersitemappings")]
        public string GetWorkerSiteMappings(TokenOnlyRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return JsonConvert.SerializeObject(_proc.GetWorkerSiteMappings(accountId), Newtonsoft.Json.Formatting.Indented);
        }

        [HttpPost("insertworkersitemapping")]
        public string InsertWorkerSiteMapping(InsertWorkerSiteMappingRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return JsonConvert.SerializeObject(_proc.InsertWorkerSiteMapping(accountId, req.WorkerId, req.SiteId), Newtonsoft.Json.Formatting.Indented);
        }

        [HttpPost("deleteworkersitemapping")]
        public string DeleteWorkerSiteMapping(IdOnlyRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return JsonConvert.SerializeObject(_proc.DeleteWorkerSiteMapping(accountId, req.Id), Newtonsoft.Json.Formatting.Indented);
        }

        [HttpPost("getmanagersforroom")]
        public string GetManagersForRoom(ManagersForRoomRequest req)
        {
            return JsonConvert.SerializeObject(_proc.GetManagersForRoom(req.RoomId, req.DepartmentId), Newtonsoft.Json.Formatting.Indented);
        }

        [HttpPost("getdepartmentsforroom")]
        public string GetDepartmentsForRoom(ManagersForRoomRequest req)
        {
            return JsonConvert.SerializeObject(_proc.GetDepartmentsForRoom(req.RoomId), Newtonsoft.Json.Formatting.Indented);
        }
    }
}