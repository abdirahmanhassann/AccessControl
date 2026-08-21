namespace AccessControl.Model
{
    public class ApiRequests
    {
        // ============================================================
        // GENERAL
        // ============================================================

        public struct TokenOnlyRequest
        {
            public string Token { get; set; }
        }

        public struct IdOnlyRequest
        {
            public string Token { get; set; }
            public int Id { get; set; }
        }


        // ============================================================
        // SITES
        // ============================================================

        public struct InsertSiteRequest
        {
            public string Token { get; set; }
            public string Name { get; set; }
            public string Address { get; set; }
            public string Reference { get; set; }
        }

        public struct UpdateSiteRequest
        {
            public string Token { get; set; }
            public int Id { get; set; }
            public string Name { get; set; }
            public string Address { get; set; }
            public string Reference { get; set; }
            public bool IsActive { get; set; }
        }


        // ============================================================
        // WORK AREAS
        // ============================================================

        public struct InsertWorkAreaRequest
        {
            public string Token { get; set; }
            public int SiteId { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
        }

        public struct UpdateWorkAreaRequest
        {
            public string Token { get; set; }
            public int Id { get; set; }
            public int SiteId { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public bool IsActive { get; set; }
        }


        // ============================================================
        // USERS
        // ============================================================

        public struct InsertUserRequest
        {
            public string Token { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string Email { get; set; }
            public string PhoneNumber { get; set; }
            public string Role { get; set; }
        }

        public struct UpdateUserRequest
        {
            public string Token { get; set; }
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string Email { get; set; }
            public string PhoneNumber { get; set; }
            public string Role { get; set; }
            public bool IsActive { get; set; }
        }


        // ============================================================
        // WORK AREA MANAGERS
        // ============================================================

        public struct InsertWorkAreaManagerRequest
        {
            public string Token { get; set; }
            public int WorkAreaId { get; set; }
            public int ManagerUserId { get; set; }
            public bool IsPrimary { get; set; }
        }

        public struct UpdateWorkAreaManagerRequest
        {
            public string Token { get; set; }
            public int Id { get; set; }
            public int WorkAreaId { get; set; }
            public int ManagerUserId { get; set; }
            public bool IsPrimary { get; set; }
        }


        // ============================================================
        // WORKERS
        // ============================================================

        public struct InsertWorkerRequest
        {
            public string Token { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string PhoneNumber { get; set; }
            public string CompanyName { get; set; }
        }

        public struct UpdateWorkerRequest
        {
            public string Token { get; set; }
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string PhoneNumber { get; set; }
            public string CompanyName { get; set; }
            public bool IsActive { get; set; }
        }


        // ============================================================
        // ROOMS
        // ============================================================

        public struct InsertRoomRequest
        {
            public string Token { get; set; }
            public int WorkAreaId { get; set; }
            public string RoomNumber { get; set; }
            public string Name { get; set; }
            public string QrCodeIdentifier { get; set; }
            public string Description { get; set; }
        }

        public struct UpdateRoomRequest
        {
            public string Token { get; set; }
            public int Id { get; set; }
            public int WorkAreaId { get; set; }
            public string RoomNumber { get; set; }
            public string Name { get; set; }
            public string QrCodeIdentifier { get; set; }
            public string Description { get; set; }
            public bool IsActive { get; set; }
        }


        // ============================================================
        // ACCESS WINDOWS
        // ============================================================

        public struct InsertAccessWindowRequest
        {
            public string Token { get; set; }
            public int WorkAreaId { get; set; }
            public int DayOfWeek { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
        }

        public struct UpdateAccessWindowRequest
        {
            public string Token { get; set; }
            public int Id { get; set; }
            public int WorkAreaId { get; set; }
            public int DayOfWeek { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
            public bool IsActive { get; set; }
        }


        // ============================================================
        // ACCESS REQUESTS
        // ============================================================

        public struct InsertAccessRequestRequest
        {
            public string Token { get; set; }
            public int WorkerId { get; set; }
            public int RoomId { get; set; }
            public string Reason { get; set; }
            public string WorkType { get; set; }
            public string Description { get; set; }
        }

        public struct UpdateAccessRequestRequest
        {
            public string Token { get; set; }
            public int Id { get; set; }
            public int WorkerId { get; set; }
            public int RoomId { get; set; }
            public string Status { get; set; }
            public string Reason { get; set; }
            public string WorkType { get; set; }
            public string Description { get; set; }
            public DateTime? ApprovedAt { get; set; }
            public DateTime? RejectedAt { get; set; }
            public DateTime? ClockedInAt { get; set; }
            public DateTime? ExpectedClockOutAt { get; set; }
            public DateTime? ClockedOutAt { get; set; }
            public DateTime? CompletedAt { get; set; }
        }


        // ============================================================
        // ACCESS REQUEST APPROVALS
        // ============================================================

        public struct InsertAccessRequestApprovalRequest
        {
            public string Token { get; set; }
            public int AccessRequestId { get; set; }
            public int ApproverUserId { get; set; }
            public string Status { get; set; }
            public string Comment { get; set; }
        }

        public struct UpdateAccessRequestApprovalRequest
        {
            public string Token { get; set; }
            public int Id { get; set; }
            public int AccessRequestId { get; set; }
            public int ApproverUserId { get; set; }
            public string Status { get; set; }
            public string Comment { get; set; }
        }


        // ============================================================
        // ACCESS PHOTOS
        // ============================================================

        public struct InsertAccessPhotoRequest
        {
            public string Token { get; set; }
            public int AccessRequestId { get; set; }
            public string StoragePath { get; set; }
            public string OriginalFileName { get; set; }
            public string ContentType { get; set; }
            public int FileSize { get; set; }
            public int UploadedByWorkerId { get; set; }
            public DateTime CapturedAt { get; set; }
            public string PhotoType { get; set; }
        }

        public struct UpdateAccessPhotoRequest
        {
            public string Token { get; set; }
            public int Id { get; set; }
            public int AccessRequestId { get; set; }
            public string StoragePath { get; set; }
            public string OriginalFileName { get; set; }
            public string ContentType { get; set; }
            public int FileSize { get; set; }
            public int UploadedByWorkerId { get; set; }
            public DateTime CapturedAt { get; set; }
            public string PhotoType { get; set; }
        }


        // ============================================================
        // OTP VERIFICATIONS
        // ============================================================

        public struct InsertOTPVerificationRequest
        {
            public string Token { get; set; }
            public int WorkerId { get; set; }
            public string PhoneNumber { get; set; }
            public string CodeHash { get; set; }
            public DateTime ExpiresAt { get; set; }
        }

        public struct UpdateOTPVerificationRequest
        {
            public string Token { get; set; }
            public int Id { get; set; }
            public int WorkerId { get; set; }
            public string PhoneNumber { get; set; }
            public string CodeHash { get; set; }
            public DateTime ExpiresAt { get; set; }
            public DateTime? VerifiedAt { get; set; }
            public int AttemptCount { get; set; }
        }


        // ============================================================
        // NOTIFICATIONS
        // ============================================================

        public struct InsertNotificationRequest
        {
            public string Token { get; set; }
            public int? UserId { get; set; }
            public int? WorkerId { get; set; }
            public int? AccessRequestId { get; set; }
            public string Type { get; set; }
            public string Channel { get; set; }
            public string Message { get; set; }
        }

        public struct UpdateNotificationRequest
        {
            public string Token { get; set; }
            public int Id { get; set; }
            public int? UserId { get; set; }
            public int? WorkerId { get; set; }
            public int? AccessRequestId { get; set; }
            public string Type { get; set; }
            public string Channel { get; set; }
            public string Message { get; set; }
            public string Status { get; set; }
            public DateTime? SentAt { get; set; }
            public DateTime? ReadAt { get; set; }
        }


        // ============================================================
        // AUDIT EVENTS
        // ============================================================

        public struct InsertAuditEventRequest
        {
            public string Token { get; set; }
            public int? AccessRequestId { get; set; }
            public int? WorkerId { get; set; }
            public int? UserId { get; set; }
            public string EventType { get; set; }
            public string Description { get; set; }
            public string IpAddress { get; set; }
            public string Metadata { get; set; }
        }
    }
}