using OptiLoad.Core.Models;

namespace OptiLoad.Core.Services
{
    public interface ISessionRepository
    {
        // Sessions
        Task<List<PackingSession>> GetSessionsByAdminAsync(int adminId);
        Task<PackingSession?>      GetSessionByIdAsync(int sessionId);
        Task<PackingSession?>      GetSessionByTokenAsync(string token);
        Task<int>                  CreateSessionAsync(int adminId, string name, string? description, string linkToken);
        Task                       UpdateSessionStatusAsync(int sessionId, string status);
        Task                       DeleteSessionAsync(int sessionId);

        // Session Users
        Task<SessionUser?> GetSessionUserByTokenAsync(string token);
        Task<int>          CreateSessionUserAsync(int sessionId, string displayName, string? email, string token);

        // Access Requests
        Task<List<AccessRequest>> GetRequestsBySessionAsync(int sessionId);
        Task<AccessRequest?>      GetRequestByIdAsync(int requestId);
        Task<AccessRequest?>      GetRequestByUserTokenAsync(string userToken);
        Task<int>                 CreateAccessRequestAsync(int sessionId, int sessionUserId);
        Task                      UpdateRequestStatusAsync(int requestId, string status);

        // Session Boxes
        Task<List<SessionBox>> GetSessionBoxesAsync(int sessionId);
        Task<int>              AddSessionBoxAsync(int sessionId, int boxId, int quantity, string addedBy);
        Task<bool>             DeleteSessionBoxAsync(int sessionBoxId, int sessionId);

        // Audit Log
        Task<List<BoxAuditLog>> GetAuditLogAsync(int sessionId);
        Task                    AddAuditLogAsync(BoxAuditLog log);
    }
}
