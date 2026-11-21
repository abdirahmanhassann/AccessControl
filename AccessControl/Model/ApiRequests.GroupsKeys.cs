namespace AccessControl.Model
{
    // Request bodies for multi-room access requests and keys.
    public partial class ApiRequests
    {
        /// <summary>One room of a multi-room request: an existing room by id, or one typed by the worker.</summary>
        public struct GroupRoomItem
        {
            public int RoomId { get; set; }
            public int WorkAreaId { get; set; }
            public string? RoomText { get; set; }
            public string? LocationKind { get; set; }
            public List<int>? KeyIds { get; set; }
        }

        public struct InsertAccessRequestGroupRequest
        {
            public string? PhoneNumber { get; set; }
            public int WorkerId { get; set; }
            public int SiteId { get; set; }
            public int DepartmentId { get; set; }
            public List<GroupRoomItem>? Rooms { get; set; }
            public string? Reason { get; set; }
            public string? WorkType { get; set; }
            public string? Description { get; set; }
            public int ApproverUserId { get; set; }
            public string? SupervisorName { get; set; }
            public DateTime? WorkFrom { get; set; }
            public DateTime? WorkTo { get; set; }
        }

        /// <summary>Which of these rooms are in blackout hours right now.</summary>
        public struct CheckRequestRoomsRequest
        {
            public int SiteId { get; set; }
            public List<GroupRoomItem>? Rooms { get; set; }
        }

        public struct GetAccessRequestGroupRequest
        {
            public string? Token { get; set; }
            public int GroupId { get; set; }
            /// <summary>Lets a worker without a staff token read their own group.</summary>
            public int WorkerId { get; set; }
        }

        public struct GetKeysRequest
        {
            public string? Token { get; set; }
            public int Id { get; set; }
            public int RoomId { get; set; }
            public int WorkAreaId { get; set; }
            public int SiteId { get; set; }
            public bool IncludeInactive { get; set; }
            /// <summary>With WorkTo: marks keys already booked or out for that time frame.</summary>
            public DateTime? WorkFrom { get; set; }
            public DateTime? WorkTo { get; set; }
        }

        public struct InsertKeyRequest
        {
            public string Token { get; set; }
            public int RoomId { get; set; }
            public string Label { get; set; }
            public string? TagNumber { get; set; }
            public string? Notes { get; set; }
        }

        public struct UpdateKeyRequest
        {
            public string Token { get; set; }
            public int Id { get; set; }
            public string Label { get; set; }
            public string? TagNumber { get; set; }
            public string? Notes { get; set; }
            public bool IsActive { get; set; }
        }

        public struct GetAccessRequestKeysRequest
        {
            public string Token { get; set; }
            public int Id { get; set; }
            public int AccessRequestId { get; set; }
            public int GroupId { get; set; }
            public string? Status { get; set; }
            /// <summary>Only keys still to hand out, still out, or missing (Approved, Issued, Missing).</summary>
            public bool Outstanding { get; set; }
        }

        /// <summary>Keyman: Issued, Returned or Missing.</summary>
        public struct UpdateAccessRequestKeyRequest
        {
            public string Token { get; set; }
            public int Id { get; set; }
            public string Status { get; set; }
            public string? Note { get; set; }
        }
    }
}
