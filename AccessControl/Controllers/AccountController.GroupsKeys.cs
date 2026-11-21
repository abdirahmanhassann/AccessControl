using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using static AccessControl.Model.ApiRequests;
using static AccessControl.Model.ApiResponses;

namespace AccessControl.Controllers
{
    // Multi-room access requests and keys.
    public partial class AccessController
    {
        private static string ToJson(GenericResponse ret) =>
            JsonConvert.SerializeObject(ret, Newtonsoft.Json.Formatting.Indented);

        private int OptionalAccount(string? token)
        {
            try
            {
                return string.IsNullOrWhiteSpace(token) ? 0 : Math.Max(0, _security.CheckToken(token));
            }
            catch
            {
                return 0;
            }
        }

        // Worker form: which rooms are in blackout hours right now. No token, like insertaccessrequest.
        [HttpPost("checkrequestrooms")]
        public string CheckRequestRooms(CheckRequestRoomsRequest req) =>
            ToJson(_proc.CheckRequestRooms(req.SiteId, req.Rooms));

        // Worker submits one request for one or more rooms (and keys). No token, like insertaccessrequest.
        [HttpPost("insertaccessrequestgroup")]
        public async Task<string> InsertAccessRequestGroup(InsertAccessRequestGroupRequest req) =>
            ToJson(await _proc.InsertAccessRequestGroup(req));

        // Staff, or the worker the group belongs to.
        [HttpPost("getaccessrequestgroup")]
        public string GetAccessRequestGroup(GetAccessRequestGroupRequest req) =>
            ToJson(_proc.GetAccessRequestGroup(OptionalAccount(req.Token), req.WorkerId, req.GroupId));

        // Keys by room / work area / site. Workers read active keys for the form; inactive ones need a token.
        [HttpPost("getkeys")]
        public string GetKeys(GetKeysRequest req) =>
            ToJson(_proc.GetKeys(req, OptionalAccount(req.Token)));

        [HttpPost("insertkey")]
        public string InsertKey(InsertKeyRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return ToJson(_proc.InsertKey(accountId, req.RoomId, req.Label, req.TagNumber, req.Notes));
        }

        [HttpPost("updatekey")]
        public string UpdateKey(UpdateKeyRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return ToJson(_proc.UpdateKey(accountId, req.Id, req.Label, req.TagNumber, req.Notes, req.IsActive));
        }

        // Keys on requests: by request, by group, or the Keyman's outstanding list.
        [HttpPost("getaccessrequestkeys")]
        public string GetAccessRequestKeys(GetAccessRequestKeysRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return ToJson(_proc.GetAccessRequestKeys(accountId, req));
        }

        // Keyman: Issued, Returned or Missing.
        [HttpPost("updateaccessrequestkey")]
        public async Task<string> UpdateAccessRequestKey(UpdateAccessRequestKeyRequest req)
        {
            string? err = Guard(req.Token, out int accountId);
            if (err != null) return err;
            return ToJson(await _proc.UpdateAccessRequestKey(accountId, req.Id, req.Status, req.Note));
        }
    }
}
