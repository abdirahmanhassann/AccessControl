using AccessControl.Helpers;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;
using static AccessControl.Helpers.ApiHelper;
using static AccessControl.Model.ApiRequests;
using static AccessControl.Model.ApiResponses;

namespace AccessControl.Processor
{
    // Multi-room access requests and keys (Database/MultiRoomKeys.sql).
    public partial class AccessProcessor
    {
        private const int MAX_ROOMS_PER_REQUEST = 20;

        /// <summary>THROW numbers the procedures use for messages meant for the user.</summary>
        private static bool IsUserSqlError(SqlException ex) => ex.Number >= 50000 && ex.Number < 50100;

        private sealed record BlackoutWindow(string Start, string End, bool Active);

        private sealed record SkippedRoom(int RoomId, string Label, string Message, List<BlackoutWindow> Windows);

        private sealed record RoomCheck(
            int RoomId,
            int WorkAreaId,
            string WorkAreaName,
            string Label,
            bool Blocked,
            string? Message,
            List<BlackoutWindow> Windows);

        /// <summary>
        /// For the worker form: which of these rooms are in blackout hours right now,
        /// with each room's blackout hours for today. Never creates rooms.
        /// </summary>
        public GenericResponse CheckRequestRooms(int siteId, List<GroupRoomItem>? rooms)
        {
            try
            {
                List<GroupRoomItem> items = rooms ?? new();
                if (items.Count == 0)
                {
                    return CreateFailureResponse("Add at least one room.");
                }

                DateTime now = DateTime.Now;
                Dictionary<int, string> areaNames = WorkAreaNames();
                List<RoomCheck> checks = new();
                foreach (GroupRoomItem item in items.Take(MAX_ROOMS_PER_REQUEST))
                {
                    int towerId = ResolveWorkAreaId(item.WorkAreaId, siteId);
                    int roomId = ExistingRoomId(item.RoomId, towerId, item.RoomText);
                    int workAreaId = roomId > 0 ? GetRoomWorkAreaId(roomId, towerId) : towerId;
                    string label = roomId > 0 ? RoomLabelById(roomId) : (item.RoomText ?? "").Trim();
                    string? blackout = GetBlackoutMessage(workAreaId, now);
                    checks.Add(new RoomCheck(
                        roomId,
                        workAreaId,
                        areaNames.TryGetValue(workAreaId, out string? name) ? name : "",
                        string.IsNullOrWhiteSpace(label) ? "Room" : label,
                        blackout != null,
                        blackout,
                        WindowsToday(workAreaId, now)));
                }

                return CreateSuccessResponseWithData(new
                {
                    Rooms = checks,
                    AnyBlocked = checks.Any(c => c.Blocked),
                    AllBlocked = checks.All(c => c.Blocked),
                });
            }
            catch (Exception ex)
            {
                _db.LogError("AccessProcessor/CheckRequestRooms", ex.Message + " " + ex.StackTrace, 0);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        /// <summary>
        /// One submission for one or more rooms. Rooms in blackout hours right now are
        /// left out (and listed back); if every room is blacked out nothing is saved.
        /// </summary>
        public async Task<GenericResponse> InsertAccessRequestGroup(InsertAccessRequestGroupRequest req)
        {
            try
            {
                if (req.ApproverUserId < 1)
                {
                    return CreateFailureResponse("A manager must be selected to approve this request.");
                }
                if (WorkerIsBlocked(req.WorkerId))
                {
                    return CreateFailureResponse(
                        "This worker is blocked from requesting access. Speak to the site manager.");
                }

                List<GroupRoomItem> items = req.Rooms ?? new();
                if (items.Count == 0)
                {
                    return CreateFailureResponse("Add at least one room.");
                }
                if (items.Count > MAX_ROOMS_PER_REQUEST)
                {
                    return CreateFailureResponse($"One request can cover up to {MAX_ROOMS_PER_REQUEST} rooms.");
                }
                if (req.WorkFrom.HasValue && req.WorkTo.HasValue && req.WorkTo <= req.WorkFrom)
                {
                    return CreateFailureResponse("The 'to' time must be after the start time.");
                }

                DateTime now = DateTime.Now;
                List<(int RoomId, string Label, List<int> KeyIds)> accepted = new();
                List<SkippedRoom> skipped = new();

                foreach (GroupRoomItem item in items)
                {
                    int towerId = ResolveWorkAreaId(item.WorkAreaId, req.SiteId);
                    int roomId = ResolveRoomId(item.RoomId, towerId, item.RoomText, item.LocationKind);
                    string typed = (item.RoomText ?? "").Trim();
                    if (roomId < 1)
                    {
                        return CreateFailureResponse(towerId < 1
                            ? "Choose the site for every room."
                            : $"Could not save room {typed} on the selected site.");
                    }
                    if (accepted.Any(a => a.RoomId == roomId))
                    {
                        continue; // the same room twice
                    }

                    string label = RoomLabelById(roomId);
                    if (string.IsNullOrWhiteSpace(label)) label = typed;

                    EnsureDepartmentRoomLink(req.DepartmentId, roomId);

                    int workAreaId = GetRoomWorkAreaId(roomId, towerId);
                    string? blackout = GetBlackoutMessage(workAreaId, now);
                    if (blackout != null)
                    {
                        skipped.Add(new SkippedRoom(roomId, label, blackout, WindowsToday(workAreaId, now)));
                        continue;
                    }

                    if (!ManagerIsAllowed(req.ApproverUserId, roomId, req.DepartmentId))
                    {
                        return CreateFailureResponse($"That manager is not assigned to {label}.");
                    }

                    accepted.Add((roomId, label, (item.KeyIds ?? new()).Where(k => k > 0).Distinct().ToList()));
                }

                if (accepted.Count == 0)
                {
                    return CreateFailureResponse(
                        "All of these rooms are in blackout hours right now, so nothing was sent. " +
                        string.Join(" ", skipped.Select(s => $"{s.Label}: {s.Message}")));
                }

                string itemsJson = JsonSerializer.Serialize(
                    accepted.Select(a => new { roomId = a.RoomId, keyIds = a.KeyIds }));

                DataTable created;
                try
                {
                    created = _db.InsertAccessRequestGroup(
                        req.WorkerId,
                        req.ApproverUserId,
                        req.Reason,
                        req.WorkType,
                        req.Description,
                        req.SupervisorName,
                        req.WorkFrom,
                        req.WorkTo,
                        itemsJson);
                }
                catch (SqlException ex) when (IsUserSqlError(ex))
                {
                    return CreateFailureResponse(ex.Message);
                }

                if (created.Rows.Count == 0)
                {
                    return CreateFailureResponse(GENERIC_ERROR);
                }

                int groupId = Convert.ToInt32(created.Rows[0]["GroupId"]);
                var requests = created.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        Id = Convert.ToInt32(r["AccessRequestId"]),
                        RoomId = Convert.ToInt32(r["RoomId"]),
                        Label = accepted.Where(a => a.RoomId == Convert.ToInt32(r["RoomId"])).Select(a => a.Label).FirstOrDefault() ?? "",
                    })
                    .ToList();

                string workerName = WorkerName(req.WorkerId);
                DataTable approverRows = _db.GetUsers(req.ApproverUserId);
                DataRow? approver = approverRows.Rows.Count > 0 ? approverRows.Rows[0] : null;
                string approverName = approver == null ? $"user #{req.ApproverUserId}" : $"{Col(approver, "FirstName")} {Col(approver, "LastName")}".Trim();
                string roomList = string.Join(", ", requests.Select(r => r.Label));

                foreach (var r in requests)
                {
                    _db.InsertAuditEvent(
                        r.Id,
                        req.WorkerId,
                        req.ApproverUserId,
                        "AccessRequested",
                        $"Worker {workerName} submitted access request #{r.Id} for {r.Label} " +
                        $"(one of {requests.Count} rooms in request group #{groupId}) for manager {approverName}.");
                }

                // One text to the manager for the whole submission.
                string managerPhone = approver == null ? "" : Col(approver, "PhoneNumber");
                if (_twilio && !string.IsNullOrWhiteSpace(managerPhone))
                {
                    try
                    {
                        DataTable roomRows = _db.GetRooms(requests[0].RoomId);
                        string site = roomRows.Rows.Count > 0 ? Col(roomRows.Rows[0], "SiteName") : "";
                        string area = roomRows.Rows.Count > 0 ? Col(roomRows.Rows[0], "WorkAreaName") : "";
                        string[] names = workerName.Split(' ', 2);
                        await new TwilioHelper().ApprovalRequestAsync(
                            names[0], names.Length > 1 ? names[1] : "", site, area, roomList, managerPhone);
                    }
                    catch (Exception smsEx)
                    {
                        _db.LogError("AccessProcessor/InsertAccessRequestGroup", "Twilio SMS skipped: " + smsEx.Message, 0);
                    }
                }

                return CreateSuccessResponseWithData(new
                {
                    GroupId = groupId,
                    Id = requests[0].Id,
                    RoomId = requests[0].RoomId,
                    Requests = requests,
                    Skipped = skipped,
                });
            }
            catch (Exception ex)
            {
                _db.LogError("AccessProcessor/InsertAccessRequestGroup", ex.Message + " " + ex.StackTrace, 0);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        /// <summary>Every room of a group with its approval, and the keys asked for.</summary>
        public GenericResponse GetAccessRequestGroup(int userId, int workerId, int groupId)
        {
            try
            {
                if (groupId < 1)
                {
                    return CreateFailureResponse("Request group not found.");
                }
                DataTable rooms = _db.GetAccessRequestGroup(groupId);
                if (rooms.Rows.Count == 0)
                {
                    return CreateFailureResponse("Request group not found.");
                }
                if (userId < 1)
                {
                    // A worker without a staff token may only read their own group.
                    bool own = workerId > 0 && rooms.Rows.Cast<DataRow>()
                        .All(r => int.TryParse(Col(r, "WorkerId"), out int w) && w == workerId);
                    if (!own)
                    {
                        return CreateFailureResponse(TOKEN_ERROR);
                    }
                }
                DataTable keys = _db.GetAccessRequestKeys(groupId: groupId);
                return CreateSuccessResponseWithData(new { GroupId = groupId, Rooms = rooms, Keys = keys });
            }
            catch (Exception ex)
            {
                _db.LogError("AccessProcessor/GetAccessRequestGroup", ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        // ---------------------------------------------------------------- keys

        /// <summary>Keys by room, work area or site; Available reflects the time frame when given.</summary>
        public GenericResponse GetKeys(GetKeysRequest req, int userId)
        {
            try
            {
                return CreateSuccessResponseWithData(_db.GetKeys(
                    req.Id, req.RoomId, req.WorkAreaId, req.SiteId,
                    req.IncludeInactive && userId > 0, req.WorkFrom, req.WorkTo));
            }
            catch (Exception ex)
            {
                _db.LogError("AccessProcessor/GetKeys", ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse InsertKey(int userId, int roomId, string? label, string? tagNumber, string? notes)
        {
            try
            {
                if (!IsManagerRole(UserRole(userId)))
                {
                    return CreateFailureResponse("Only managers and Keymen can manage keys.");
                }
                label = (label ?? "").Trim();
                if (roomId < 1 || label.Length == 0)
                {
                    return CreateFailureResponse("Choose the room and give the key a name.");
                }
                int id;
                try
                {
                    id = _db.InsertKey(roomId, label, tagNumber, notes);
                }
                catch (SqlException ex) when (ex.Number is 2627 or 2601)
                {
                    return CreateFailureResponse($"That room already has a key called {label}.");
                }
                _db.InsertAuditEvent(null, null, userId, "KeyAdded", $"Key {label} added to {RoomLabelById(roomId)}.");
                return CreateSuccessResponseWithData(new { Id = id });
            }
            catch (Exception ex)
            {
                _db.LogError("AccessProcessor/InsertKey", ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse UpdateKey(int userId, int id, string? label, string? tagNumber, string? notes, bool isActive)
        {
            try
            {
                if (!IsManagerRole(UserRole(userId)))
                {
                    return CreateFailureResponse("Only managers and Keymen can manage keys.");
                }
                label = (label ?? "").Trim();
                if (id < 1 || label.Length == 0)
                {
                    return CreateFailureResponse("Give the key a name.");
                }
                try
                {
                    if (!_db.UpdateKey(id, label, tagNumber, notes, isActive))
                    {
                        return CreateFailureResponse("That key no longer exists.");
                    }
                }
                catch (SqlException ex) when (ex.Number is 2627 or 2601)
                {
                    return CreateFailureResponse($"That room already has a key called {label}.");
                }
                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError("AccessProcessor/UpdateKey", ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        public GenericResponse GetAccessRequestKeys(int userId, GetAccessRequestKeysRequest req)
        {
            try
            {
                return CreateSuccessResponseWithData(_db.GetAccessRequestKeys(
                    req.Id, req.AccessRequestId, req.GroupId,
                    string.IsNullOrWhiteSpace(req.Status) ? null : NormalizeKeyStatus(req.Status),
                    req.Outstanding));
            }
            catch (Exception ex)
            {
                _db.LogError("AccessProcessor/GetAccessRequestKeys", ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        private static string NormalizeKeyStatus(string status) =>
            status.Trim().ToLowerInvariant() switch
            {
                "issue" or "issued" => "Issued",
                "return" or "returned" => "Returned",
                "missing" or "lost" => "Missing",
                "approved" => "Approved",
                "rejected" => "Rejected",
                _ => "Requested",
            };

        /// <summary>
        /// The Keyman hands a key over (Issued), takes it back (Returned) or records it as Missing.
        /// Missing tells the approving managers; the Keyman then rejects the room leave if needed.
        /// </summary>
        public async Task<GenericResponse> UpdateAccessRequestKey(int userId, int id, string? status, string? note)
        {
            try
            {
                if (!CanReviewRoomLeave(userId))
                {
                    return CreateFailureResponse("Only a Keyman can hand out or take back keys.");
                }
                string next = NormalizeKeyStatus(status ?? "");
                if (next is not ("Issued" or "Returned" or "Missing"))
                {
                    return CreateFailureResponse("Choose issued, returned or missing.");
                }

                DataRow? key = _db.GetAccessRequestKeys(id: id).Rows.Cast<DataRow>().FirstOrDefault();
                if (key == null)
                {
                    return CreateFailureResponse("That key booking no longer exists.");
                }
                if (!_db.UpdateAccessRequestKey(id, next, userId, note))
                {
                    string current = Col(key, "Status");
                    return CreateFailureResponse(next switch
                    {
                        "Issued" => $"This key cannot be handed out: it is {current.ToLowerInvariant()}.",
                        "Returned" => $"This key cannot be returned: it is {current.ToLowerInvariant()}.",
                        _ => $"Only a key that has been handed out can be marked missing (it is {current.ToLowerInvariant()}).",
                    });
                }

                int accessRequestId = int.TryParse(Col(key, "AccessRequestId"), out int rid) ? rid : 0;
                int? workerId = int.TryParse(Col(key, "WorkerId"), out int wid) ? wid : null;
                string keyLabel = Col(key, "KeyLabel");
                string room = RoomText(key);
                string worker = $"{Col(key, "WorkerFirstName")} {Col(key, "WorkerLastName")}".Trim();
                string keyman = UserName(userId);

                string what = next switch
                {
                    "Issued" => $"Keyman {keyman} handed key {keyLabel} for {room} to {worker}.",
                    "Returned" => $"Keyman {keyman} took back key {keyLabel} for {room} from {worker}.",
                    _ => $"Keyman {keyman} recorded key {keyLabel} for {room} as missing (last with {worker})." +
                         (string.IsNullOrWhiteSpace(note) ? "" : $" Note: {note!.Trim()}"),
                };
                _db.InsertAuditEvent(accessRequestId > 0 ? accessRequestId : null, workerId, userId, "Key" + next, what);

                if (next == "Missing" && accessRequestId > 0)
                {
                    foreach (DataRow approval in _db.GetAccessRequestApprovals(0, accessRequestId, 0, null, null, 0, 0).Rows)
                    {
                        if (int.TryParse(Col(approval, "ApproverUserId"), out int approverId) && approverId > 0)
                        {
                            _db.InsertNotification(approverId, null, accessRequestId, "KeyMissing", "IN_APP", what);
                        }
                    }
                    string phone = Col(key, "WorkerPhoneNumber");
                    if (_twilio && !string.IsNullOrWhiteSpace(phone))
                    {
                        try
                        {
                            await new TwilioHelper().SendPlainMessageAsync(
                                phone, $"Key {keyLabel} for {room} has not been returned. Please bring it back to the Keyman.");
                        }
                        catch (Exception smsEx)
                        {
                            _db.LogError("AccessProcessor/UpdateAccessRequestKey", "Twilio SMS skipped: " + smsEx.Message, userId);
                        }
                    }
                }

                return CreateSuccessResponseNoData();
            }
            catch (Exception ex)
            {
                _db.LogError("AccessProcessor/UpdateAccessRequestKey", ex.Message + " " + ex.StackTrace, userId);
                return CreateFailureResponse(GENERIC_ERROR);
            }
        }

        // ------------------------------------------------- group decision message

        /// <summary>
        /// Called after a manager decides one room of a group. Once every room has a
        /// decision, sends the worker one text covering all of them (with keys and when
        /// to return each), and tells the Keyman which keys to hand out. Sent once only.
        /// </summary>
        private async Task NotifyGroupDecidedAsync(int groupId)
        {
            try
            {
                if (groupId < 1 || !_db.ClaimGroupNotification(groupId))
                {
                    return;
                }

                List<DataRow> rooms = _db.GetAccessRequestGroup(groupId).Rows.Cast<DataRow>().ToList();
                if (rooms.Count == 0) return;
                List<DataRow> keys = _db.GetAccessRequestKeys(groupId: groupId).Rows.Cast<DataRow>().ToList();

                bool IsStatus(DataRow r, string s) => Col(r, "Status").Equals(s, StringComparison.OrdinalIgnoreCase);
                List<DataRow> approved = rooms.Where(r => IsStatus(r, "Approved")).ToList();
                List<DataRow> declined = rooms.Where(r => !IsStatus(r, "Approved")).ToList();
                List<DataRow> approvedKeys = keys.Where(k => Col(k, "Status").Equals("Approved", StringComparison.OrdinalIgnoreCase)).ToList();

                string Label(DataRow r) => RoomText(r);
                string Declined(DataRow r)
                {
                    string comment = Col(r, "Comment");
                    return string.IsNullOrWhiteSpace(comment) ? Label(r) : $"{Label(r)} ({comment.Trim()})";
                }
                string KeyLine(DataRow k)
                {
                    string returnBy = DateTime.TryParse(Col(k, "ReturnBy"), out DateTime when)
                        ? $"return by {when:HH:mm ddd d MMM}"
                        : "return when you leave";
                    return $"{Col(k, "KeyLabel")} for {RoomText(k)}, {returnBy}";
                }

                string message =
                    declined.Count == 0 ? $"Your access request is approved for {string.Join(", ", approved.Select(Label))}."
                    : approved.Count == 0 ? $"Your access request was declined for {string.Join(", ", declined.Select(Declined))}."
                    : $"Your access request has been reviewed. Approved: {string.Join(", ", approved.Select(Label))}. " +
                      $"Declined: {string.Join(", ", declined.Select(Declined))}.";
                if (approvedKeys.Count > 0)
                {
                    message += $" Collect from the Keyman: {string.Join("; ", approvedKeys.Select(KeyLine))}.";
                }

                DataRow first = rooms[0];
                int workerId = int.TryParse(Col(first, "WorkerId"), out int wid) ? wid : 0;
                string workerName = $"{Col(first, "WorkerFirstName")} {Col(first, "WorkerLastName")}".Trim();

                // The worker's waiting screen looks these up per room.
                foreach (DataRow r in rooms)
                {
                    if (int.TryParse(Col(r, "AccessRequestId"), out int rid))
                    {
                        _db.InsertNotification(null, workerId > 0 ? workerId : null, rid,
                            IsStatus(r, "Approved") ? "Approved" : "Rejected", "SMS", message);
                    }
                }

                string phone = Col(first, "WorkerPhoneNumber");
                if (_twilio && !string.IsNullOrWhiteSpace(phone))
                {
                    try
                    {
                        await new TwilioHelper().SendPlainMessageAsync(phone, message);
                    }
                    catch (Exception smsEx)
                    {
                        _db.LogError("AccessProcessor/NotifyGroupDecidedAsync", "Twilio SMS skipped: " + smsEx.Message, 0);
                    }
                }

                if (approvedKeys.Count == 0) return;

                // The Keyman: which keys to have ready, for whom, and when they come back.
                int firstKeyRoom = int.TryParse(Col(approvedKeys[0], "RoomId"), out int kr) ? kr : 0;
                int firstKeyRequest = int.TryParse(Col(approvedKeys[0], "AccessRequestId"), out int kq) ? kq : 0;
                string company = Col(approvedKeys[0], "CompanyName");
                string keymanMessage =
                    $"Keys to hand out to {workerName}{(string.IsNullOrWhiteSpace(company) ? "" : $" ({company})")}: " +
                    $"{string.Join("; ", approvedKeys.Select(KeyLine))}.";
                foreach (DataRow keyman in KeymenForRoom(firstKeyRoom))
                {
                    if (!int.TryParse(Col(keyman, "Id"), out int keymanId)) continue;
                    _db.InsertNotification(keymanId, null, firstKeyRequest > 0 ? firstKeyRequest : null, "KeysToIssue", "IN_APP", keymanMessage);
                    string keymanPhone = Col(keyman, "PhoneNumber");
                    if (_twilio && !string.IsNullOrWhiteSpace(keymanPhone))
                    {
                        try
                        {
                            await new TwilioHelper().SendPlainMessageAsync(keymanPhone, keymanMessage + " Open SiteGate > Keyman > Keys.");
                        }
                        catch (Exception smsEx)
                        {
                            _db.LogError("AccessProcessor/NotifyGroupDecidedAsync", "Twilio SMS skipped: " + smsEx.Message, keymanId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _db.LogError("AccessProcessor/NotifyGroupDecidedAsync", ex.Message + " " + ex.StackTrace, 0);
            }
        }

        // ----------------------------------------------------------- helpers

        /// <summary>An existing room by id, or one already on the work area matching the typed text. 0 if none.</summary>
        private int ExistingRoomId(int roomId, int workAreaId, string? roomText)
        {
            if (roomId > 0 && _db.GetRooms(roomId).Rows.Count > 0) return roomId;
            string typed = (roomText ?? "").Trim();
            if (workAreaId < 1 || typed.Length == 0) return 0;
            return MatchRoomId(_db.GetRooms(null, workAreaId), typed);
        }

        private string RoomLabelById(int roomId)
        {
            DataTable dt = _db.GetRooms(roomId);
            if (dt.Rows.Count == 0) return "";
            DataRow row = dt.Rows[0];
            string number = Col(row, "RoomNumber");
            string name = Col(row, "RoomName", "Name");
            if (string.IsNullOrWhiteSpace(name) || name == number) return number;
            return name.Contains(number, StringComparison.OrdinalIgnoreCase) ? name : $"{number} {name}".Trim();
        }

        private Dictionary<int, string> WorkAreaNames()
        {
            Dictionary<int, string> names = new();
            foreach (DataRow row in _db.GetWorkAreas().Rows)
            {
                if (int.TryParse(Col(row, "Id"), out int id)) names[id] = Col(row, "Name", "WorkAreaName");
            }
            return names;
        }

        /// <summary>Today's blackout hours for a work area, and whether each is in force right now.</summary>
        private List<BlackoutWindow> WindowsToday(int workAreaId, DateTime now)
        {
            List<BlackoutWindow> result = new();
            foreach (DataRow row in ActiveWindows(workAreaId, now.DayOfWeek))
            {
                TimeSpan start = ToTimeSpan(row["StartTime"]);
                TimeSpan end = ToTimeSpan(row["EndTime"]);
                TimeSpan t = now.TimeOfDay;
                bool active = start <= end ? t >= start && t < end : t >= start || t < end;
                result.Add(new BlackoutWindow($"{start:hh\\:mm}", $"{end:hh\\:mm}", active));
            }
            return result.OrderBy(w => w.Start).ToList();
        }

        private string WorkerName(int workerId)
        {
            DataTable dt = _db.GetWorkers(workerId, null);
            return dt.Rows.Count > 0 ? $"{Col(dt.Rows[0], "FirstName")} {Col(dt.Rows[0], "LastName")}".Trim() : $"#{workerId}";
        }

        private string UserName(int userId)
        {
            DataTable dt = _db.GetUsers(userId);
            return dt.Rows.Count > 0 ? $"{Col(dt.Rows[0], "FirstName")} {Col(dt.Rows[0], "LastName")}".Trim() : $"User #{userId}";
        }
    }
}
