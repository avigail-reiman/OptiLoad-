using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OptiLoad.Core.Models;
using OptiLoad.Core.Services;
using OptiLoad.Data;
using System.Security.Claims;
using System.Security.Cryptography;

namespace OptiLoad.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionController : ControllerBase
    {
        private readonly ISessionRepository _sessions;
        private readonly DatabaseService    _db;

        public SessionController(ISessionRepository sessions, DatabaseService db)
        {
            _sessions = sessions;
            _db       = db;
        }

        // ─── helpers ──────────────────────────────────────────────────────────

        private int    GetAdminId()       => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private string GetAdminUsername() => User.FindFirstValue(ClaimTypes.Name)!;

        /// <summary>Generates a cryptographically random 36-char token (GUID-shaped).</summary>
        private static string NewToken()
        {
            var bytes = new byte[16];
            RandomNumberGenerator.Fill(bytes);
            return new Guid(bytes).ToString();
        }

        // Checks: valid session + (admin owner OR approved session-user)
        private async Task<(bool ok, IActionResult? err)> AuthorizeAccess(int sessionId)
        {
            var session = await _sessions.GetSessionByIdAsync(sessionId);
            if (session == null) return (false, NotFound());

            if (User.Identity?.IsAuthenticated == true)
            {
                if (session.AdminId != GetAdminId()) return (false, Forbid());
                return (true, null);
            }

            var userToken = Request.Headers["X-User-Token"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(userToken)) return (false, Unauthorized());

            var req = await _sessions.GetRequestByUserTokenAsync(userToken);
            if (req == null || req.SessionId != sessionId || req.Status != "Approved")
                return (false, Forbid());

            return (true, null);
        }

        // Same, but also returns actor name + type for audit
        private async Task<(bool ok, IActionResult? err, string? actor, string? actorType)>
            AuthorizeAccessWithActor(int sessionId)
        {
            var session = await _sessions.GetSessionByIdAsync(sessionId);
            if (session == null) return (false, NotFound(), null, null);

            if (User.Identity?.IsAuthenticated == true)
            {
                if (session.AdminId != GetAdminId()) return (false, Forbid(), null, null);
                return (true, null, GetAdminUsername(), "Admin");
            }

            var userToken = Request.Headers["X-User-Token"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(userToken)) return (false, Unauthorized(), null, null);

            var req = await _sessions.GetRequestByUserTokenAsync(userToken);
            if (req == null || req.SessionId != sessionId || req.Status != "Approved")
                return (false, Forbid(), null, null);

            return (true, null, req.DisplayName ?? "User", "User");
        }

        // ─── Admin: Sessions CRUD ─────────────────────────────────────────────

        /// <summary>כל התוכניות של המנהל המחובר</summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetMySessions()
        {
            var list = await _sessions.GetSessionsByAdminAsync(GetAdminId());
            return Ok(list);
        }

        /// <summary>יצירת תוכנית שיבוץ חדשה</summary>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest("Name is required");
            if (req.Name.Length > 100)
                return BadRequest("Name must be 100 characters or fewer");
            if (req.Description?.Length > 500)
                return BadRequest("Description must be 500 characters or fewer");

            var linkToken = NewToken();
            var id        = await _sessions.CreateSessionAsync(GetAdminId(), req.Name.Trim(), req.Description?.Trim(), linkToken);
            var session   = await _sessions.GetSessionByIdAsync(id);
            return CreatedAtAction(nameof(GetSession), new { id }, session);
        }

        /// <summary>פרטי תוכנית שיבוץ</summary>
        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetSession(int id)
        {
            var session = await _sessions.GetSessionByIdAsync(id);
            if (session == null) return NotFound();
            if (session.AdminId != GetAdminId()) return Forbid();
            return Ok(session);
        }

        /// <summary>עדכון סטטוס (Open / Closed)</summary>
        [Authorize]
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest req)
        {
            var session = await _sessions.GetSessionByIdAsync(id);
            if (session == null) return NotFound();
            if (session.AdminId != GetAdminId()) return Forbid();
            if (req.Status != "Open" && req.Status != "Closed")
                return BadRequest("Status must be Open or Closed");

            await _sessions.UpdateSessionStatusAsync(id, req.Status);
            return NoContent();
        }

        /// <summary>מחיקת תוכנית שיבוץ (כולל כל הנתונים שלה)</summary>
        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteSession(int id)
        {
            var session = await _sessions.GetSessionByIdAsync(id);
            if (session == null) return NotFound();
            if (session.AdminId != GetAdminId()) return Forbid();
            await _sessions.DeleteSessionAsync(id);
            return NoContent();
        }

        // ─── Public: Join via link ────────────────────────────────────────────

        /// <summary>קבלת מידע על תוכנית דרך קישור (ציבורי)</summary>
        [AllowAnonymous]
        [HttpGet("link/{token}")]
        public async Task<IActionResult> GetByLink(string token)
        {
            var session = await _sessions.GetSessionByTokenAsync(token);
            if (session == null) return NotFound();
            return Ok(new { session.SessionId, session.Name, session.Description, session.Status });
        }

        /// <summary>בקשת הרשאה לכניסה לתוכנית (ציבורי)</summary>
        [AllowAnonymous]
        [HttpPost("link/{token}/join")]
        public async Task<IActionResult> RequestAccess(string token, [FromBody] JoinRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.DisplayName))
                return BadRequest("DisplayName is required");
            if (req.DisplayName.Length > 100)
                return BadRequest("DisplayName must be 100 characters or fewer");
            if (req.Email != null && req.Email.Length > 200)
                return BadRequest("Email must be 200 characters or fewer");

            var session = await _sessions.GetSessionByTokenAsync(token);
            if (session == null) return NotFound("Session not found");
            if (session.Status != "Open")  return BadRequest("Session is closed");

            // ── DDoS / spam guard ────────────────────────────────────────────
            if (req.PreviousToken != null)
            {
                var prevRequest = await _sessions.GetRequestByUserTokenAsync(req.PreviousToken);
                if (prevRequest == null || prevRequest.SessionId != session.SessionId || prevRequest.Status != "Denied")
                    return BadRequest("previousToken is invalid or does not belong to a denied request for this session.");

                var rootToken    = await _sessions.GetRootTokenAsync(req.PreviousToken);
                int deniedCount  = rootToken != null
                    ? await _sessions.GetDeniedCountInChainAsync(rootToken, session.SessionId)
                    : 0;

                if (deniedCount >= 2)
                    return StatusCode(429, "Too many denied requests. You are permanently blocked from this session.");
            }

            var userToken = NewToken();
            var userId    = await _sessions.CreateSessionUserAsync(
                                session.SessionId, req.DisplayName.Trim(), req.Email?.Trim(), userToken,
                                req.PreviousToken);
            await _sessions.CreateAccessRequestAsync(session.SessionId, userId);

            return Ok(new { userToken, message = "Request submitted, waiting for admin approval" });
        }

        /// <summary>בדיקת סטטוס בקשה (ציבורי — polling)</summary>
        [AllowAnonymous]
        [HttpGet("link/{token}/status")]
        public async Task<IActionResult> CheckStatus(string token, [FromQuery] string userToken)
        {
            if (string.IsNullOrWhiteSpace(userToken)) return BadRequest("userToken required");

            var session = await _sessions.GetSessionByTokenAsync(token);
            if (session == null) return NotFound();

            var req = await _sessions.GetRequestByUserTokenAsync(userToken);
            if (req == null || req.SessionId != session.SessionId) return NotFound();

            return Ok(new { req.Status, req.DisplayName, sessionId = session.SessionId });
        }

        // ─── Admin: Access Requests ───────────────────────────────────────────

        /// <summary>רשימת בקשות הרשאה לתוכנית</summary>
        [Authorize]
        [HttpGet("{id:int}/requests")]
        public async Task<IActionResult> GetRequests(int id)
        {
            var session = await _sessions.GetSessionByIdAsync(id);
            if (session == null) return NotFound();
            if (session.AdminId != GetAdminId()) return Forbid();

            var requests = await _sessions.GetRequestsBySessionAsync(id);
            return Ok(requests);
        }

        /// <summary>אישור בקשת הרשאה</summary>
        [Authorize]
        [HttpPut("{id:int}/requests/{reqId:int}/approve")]
        public async Task<IActionResult> ApproveRequest(int id, int reqId)
        {
            var session = await _sessions.GetSessionByIdAsync(id);
            if (session == null) return NotFound();
            if (session.AdminId != GetAdminId()) return Forbid();

            var req = await _sessions.GetRequestByIdAsync(reqId);
            if (req == null || req.SessionId != id) return NotFound();

            await _sessions.UpdateRequestStatusAsync(reqId, "Approved");
            return NoContent();
        }

        /// <summary>דחיית בקשת הרשאה</summary>
        [Authorize]
        [HttpPut("{id:int}/requests/{reqId:int}/deny")]
        public async Task<IActionResult> DenyRequest(int id, int reqId)
        {
            var session = await _sessions.GetSessionByIdAsync(id);
            if (session == null) return NotFound();
            if (session.AdminId != GetAdminId()) return Forbid();

            var req = await _sessions.GetRequestByIdAsync(reqId);
            if (req == null || req.SessionId != id) return NotFound();

            await _sessions.UpdateRequestStatusAsync(reqId, "Denied");
            return NoContent();
        }

        // ─── Boxes (Admin OR Approved User) ──────────────────────────────────

        /// <summary>קטלוג הארגזים הזמינים (לבחירה בטופס הוספה)</summary>
        [HttpGet("{id:int}/catalog")]
        public async Task<IActionResult> GetCatalog(int id)
        {
            var (ok, err) = await AuthorizeAccess(id);
            if (!ok) return err!;

            var boxes = await _db.GetAllBoxes();
            return Ok(boxes);
        }

        /// <summary>ארגזים שהוכנסו לתוכנית</summary>
        [HttpGet("{id:int}/boxes")]
        public async Task<IActionResult> GetBoxes(int id)
        {
            var (ok, err) = await AuthorizeAccess(id);
            if (!ok) return err!;

            var boxes = await _sessions.GetSessionBoxesAsync(id);
            return Ok(boxes);
        }

        /// <summary>הוספת ארגז לתוכנית + רישום אודיט</summary>
        [HttpPost("{id:int}/boxes")]
        public async Task<IActionResult> AddBox(int id, [FromBody] AddBoxRequest req)
        {
            var (ok, err, actor, actorType) = await AuthorizeAccessWithActor(id);
            if (!ok) return err!;

            if (req.Quantity <= 0) return BadRequest("Quantity must be positive");

            var box = await _db.GetBoxById(req.BoxId);
            if (box == null) return NotFound("Box not found in catalog");

            var sessionBoxId = await _sessions.AddSessionBoxAsync(id, req.BoxId, req.Quantity, actor!);

            await _sessions.AddAuditLogAsync(new BoxAuditLog
            {
                SessionId     = id,
                Action        = "Added",
                BoxId         = req.BoxId,
                BoxName       = box.BoxName,
                Quantity      = req.Quantity,
                ChangedBy     = actor!,
                ChangedByType = actorType!,
                ChangedAt     = DateTime.UtcNow
            });

            return Ok(new { sessionBoxId });
        }

        /// <summary>מחיקת ארגז מהתוכנית + רישום אודיט</summary>
        [HttpDelete("{id:int}/boxes/{sessionBoxId:int}")]
        public async Task<IActionResult> DeleteBox(int id, int sessionBoxId)
        {
            var (ok, err, actor, actorType) = await AuthorizeAccessWithActor(id);
            if (!ok) return err!;

            var boxes = await _sessions.GetSessionBoxesAsync(id);
            var box   = boxes.FirstOrDefault(b => b.SessionBoxId == sessionBoxId);
            if (box == null) return NotFound();

            var deleted = await _sessions.DeleteSessionBoxAsync(sessionBoxId, id);
            if (!deleted) return NotFound();

            await _sessions.AddAuditLogAsync(new BoxAuditLog
            {
                SessionId     = id,
                Action        = "Deleted",
                BoxId         = box.BoxId,
                BoxName       = box.BoxName ?? string.Empty,
                Quantity      = box.Quantity,
                ChangedBy     = actor!,
                ChangedByType = actorType!,
                ChangedAt     = DateTime.UtcNow
            });

            return NoContent();
        }

        /// <summary>לוג שינויים (מנהל בלבד)</summary>
        [Authorize]
        [HttpGet("{id:int}/audit")]
        public async Task<IActionResult> GetAuditLog(int id)
        {
            var session = await _sessions.GetSessionByIdAsync(id);
            if (session == null) return NotFound();
            if (session.AdminId != GetAdminId()) return Forbid();

            var log = await _sessions.GetAuditLogAsync(id);
            return Ok(log);
        }

        /// <summary>הוספת ארגז חדש לתוכנית (הזנה ידנית, ללא קטלוג)</summary>
        [HttpPost("{id:int}/boxes/custom")]
        public async Task<IActionResult> AddCustomBox(int id, [FromBody] AddCustomBoxRequest req)
        {
            var (ok, err, actor, actorType) = await AuthorizeAccessWithActor(id);
            if (!ok) return err!;

            if (string.IsNullOrWhiteSpace(req.BoxName)) return BadRequest("BoxName is required");
            if (req.BoxName.Length > 100) return BadRequest("BoxName must be 100 characters or fewer");
            if (req.Width <= 0 || req.Height <= 0 || req.Depth <= 0) return BadRequest("Dimensions must be positive");
            if (req.Width > 100000 || req.Height > 100000 || req.Depth > 100000) return BadRequest("Dimensions are unreasonably large");
            if (req.WeightKg < 0) return BadRequest("WeightKg must be non-negative");
            if (req.WeightKg > 100000) return BadRequest("WeightKg is unreasonably large");
            if (req.Quantity <= 0) return BadRequest("Quantity must be positive");
            if (req.Quantity > 10000) return BadRequest("Quantity is unreasonably large");

            var box = new Box
            {
                BoxName       = req.BoxName.Trim(),
                Width         = req.Width,
                Height        = req.Height,
                Depth         = req.Depth,
                WeightKg      = req.WeightKg,
                IsFragile     = req.IsFragile,
                AllowRotation = req.AllowRotation,
                CreatedAt     = DateTime.UtcNow
            };
            var boxId = await _db.CreateBox(box);

            var sessionBoxId = await _sessions.AddSessionBoxAsync(id, boxId, req.Quantity, actor!);

            await _sessions.AddAuditLogAsync(new BoxAuditLog
            {
                SessionId     = id,
                Action        = "Added",
                BoxId         = boxId,
                BoxName       = box.BoxName,
                Quantity      = req.Quantity,
                ChangedBy     = actor!,
                ChangedByType = actorType!,
                ChangedAt     = DateTime.UtcNow
            });

            return Ok(new { sessionBoxId });
        }

        /// <summary>עדכון כמות ארגז בתוכנית + רישום אודיט</summary>
        [HttpPut("{id:int}/boxes/{sessionBoxId:int}")]
        public async Task<IActionResult> UpdateBox(int id, int sessionBoxId, [FromBody] UpdateBoxRequest req)
        {
            var (ok, err, actor, actorType) = await AuthorizeAccessWithActor(id);
            if (!ok) return err!;

            if (req.Quantity <= 0) return BadRequest("Quantity must be positive");
            if (req.Quantity > 10000) return BadRequest("Quantity is unreasonably large");

            var boxes = await _sessions.GetSessionBoxesAsync(id);
            var box   = boxes.FirstOrDefault(b => b.SessionBoxId == sessionBoxId);
            if (box == null) return NotFound();

            var updated = await _sessions.UpdateSessionBoxQuantityAsync(sessionBoxId, id, req.Quantity);
            if (!updated) return NotFound();

            await _sessions.AddAuditLogAsync(new BoxAuditLog
            {
                SessionId     = id,
                Action        = "Updated",
                BoxId         = box.BoxId,
                BoxName       = box.BoxName ?? string.Empty,
                Quantity      = req.Quantity,
                ChangedBy     = actor!,
                ChangedByType = actorType!,
                ChangedAt     = DateTime.UtcNow
            });

            return NoContent();
        }

        // ─── DTOs ─────────────────────────────────────────────────────────────

        public record CreateSessionRequest(string Name, string? Description);
        public record JoinRequest(string DisplayName, string? Email, string? PreviousToken);
        public record AddBoxRequest(int BoxId, int Quantity);
        public record AddCustomBoxRequest(string BoxName, double Width, double Height, double Depth, double WeightKg, bool IsFragile, bool AllowRotation, int Quantity);
        public record UpdateBoxRequest(int Quantity);
        public record UpdateStatusRequest(string Status);
    }
}
