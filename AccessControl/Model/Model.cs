namespace AccessControl.Model
{
    public class Model
    {
        // ============================================================
        // SITE
        // ============================================================

        public class Site
        {
            public int Id;
            public string Name;
            public string Address;
            public string Reference;
            public bool IsActive;
            public DateTime CreatedAt;
        }

        public class ScannedRoom
        {
            public int Id;
            public int WorkAreaId;
            public string RoomNumber;
            public string Name;
            public string QrCodeIdentifier;
            public string Description;
            public bool IsActive;
            public DateTime CreatedAt;

            public string WorkAreaName;
            public string WorkAreaDescription;

            public int SiteId;
            public string SiteName;
            public string SiteAddress;
            public string SiteReference;
        }


        public class WorkArea
        {
            public int Id;
            public int SiteId;
            public string Name;
            public string Description;
            public bool IsActive;
            public DateTime CreatedAt;
        }



        public class Room
        {
            public int Id;
            public int WorkAreaId;
            public string RoomNumber;
            public string Name;
            public string QrCodeIdentifier;
            public string Description;
            public bool IsActive;
            public DateTime CreatedAt;
        }




        public class User
        {
            public int Id;
            public string FirstName;
            public string LastName;
            public string Email;
            public string PhoneNumber;
            public string Role;
            public bool IsActive;
            public DateTime CreatedAt;
            public DateTime? LastLoginAt;
        }


        public class WorkAreaManager
        {
            public int Id;
            public int WorkAreaId;
            public int UserId;
            public bool IsPrimary;
            public DateTime AssignedAt;
        }


        public class Worker
        {
            public int Id;
            public string FirstName;
            public string LastName;
            public string PhoneNumber;
            public string CompanyName;
            public bool IsActive;
            public DateTime CreatedAt;
            public DateTime? LastSeenAt;
        }


        // ============================================================
        // ACCESS WINDOW
        // ============================================================

        public class AccessWindow
        {
            public int Id;
            public int WorkAreaId;
            public int DayOfWeek;
            public TimeSpan StartTime;
            public TimeSpan EndTime;
            public bool IsActive;
        }


        // ============================================================
        // ACCESS REQUEST
        // ============================================================

        public class AccessRequest
        {
            public int Id;
            public int WorkerId;
            public int RoomId;
            public string Status;
            public string Reason;
            public string WorkType;
            public string Description;

            public DateTime RequestedAt;
            public DateTime? ApprovedAt;
            public DateTime? RejectedAt;

            public DateTime? ClockedInAt;
            public DateTime? ExpectedClockOutAt;
            public DateTime? ClockedOutAt;
            public DateTime? CompletedAt;

            public DateTime CreatedAt;
        }


        // ============================================================
        // ACCESS REQUEST APPROVAL
        // ============================================================

        public class AccessRequestApproval
        {
            public int Id;
            public int AccessRequestId;
            public int ApproverUserId;
            public string Status;
            public string Comment;
            public DateTime? ReviewedAt;
        }


        // ============================================================
        // ACCESS PHOTO
        // ============================================================

        public class AccessPhoto
        {
            public int Id;
            public int AccessRequestId;
            public string StoragePath;
            public string OriginalFileName;
            public string ContentType;
            public int FileSize;
            public int UploadedByWorkerId;
            public DateTime CapturedAt;
            public DateTime UploadedAt;
            public string PhotoType;
        }


        // ============================================================
        // OTP VERIFICATION
        // ============================================================

        public class OTPVerification
        {
            public int Id;
            public int WorkerId;
            public string PhoneNumber;
            public string CodeHash;
            public DateTime ExpiresAt;
            public DateTime? VerifiedAt;
            public int AttemptCount;
            public DateTime CreatedAt;
        }


        // ============================================================
        // NOTIFICATION
        // ============================================================

        public class Notification
        {
            public int Id;
            public int? UserId;
            public int? WorkerId;
            public int? AccessRequestId;
            public string Type;
            public string Channel;
            public string Message;
            public string Status;
            public DateTime? SentAt;
            public DateTime? ReadAt;
            public DateTime CreatedAt;
        }


        // ============================================================
        // AUDIT EVENT
        // ============================================================

        public class AuditEvent
        {
            public int Id;
            public int? AccessRequestId;
            public int? WorkerId;
            public int? UserId;
            public string EventType;
            public string Description;
            public DateTime CreatedAt;
            public string IpAddress;
            public string Metadata;
        }
    }
}