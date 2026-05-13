using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using OptiLoad.Core.Models;
using OptiLoad.Core.Services;

namespace OptiLoad.Data
{
    public partial class DatabaseService : ISessionRepository
    {
        // ═══════════════════════════════════════════════════════
        // Sessions
        // ═══════════════════════════════════════════════════════

        public async Task<List<PackingSession>> GetSessionsByAdminAsync(int adminId)
        {
            var list = new List<PackingSession>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "SELECT SessionId, AdminId, Name, Description, LinkToken, Status, CreatedAt " +
                "FROM PackingSession WHERE AdminId = @AdminId ORDER BY CreatedAt DESC", conn);
            cmd.Parameters.AddWithValue("@AdminId", adminId);
            await conn.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(MapSession(rdr));
            return list;
        }

        public async Task<PackingSession?> GetSessionByIdAsync(int sessionId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "SELECT SessionId, AdminId, Name, Description, LinkToken, Status, CreatedAt " +
                "FROM PackingSession WHERE SessionId = @SessionId", conn);
            cmd.Parameters.AddWithValue("@SessionId", sessionId);
            await conn.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();
            if (!await rdr.ReadAsync()) return null;
            return MapSession(rdr);
        }

        public async Task<PackingSession?> GetSessionByTokenAsync(string token)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "SELECT SessionId, AdminId, Name, Description, LinkToken, Status, CreatedAt " +
                "FROM PackingSession WHERE LinkToken = @Token", conn);
            cmd.Parameters.AddWithValue("@Token", token);
            await conn.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();
            if (!await rdr.ReadAsync()) return null;
            return MapSession(rdr);
        }

        public async Task<int> CreateSessionAsync(int adminId, string name, string? description, string linkToken)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "INSERT INTO PackingSession (AdminId, Name, Description, LinkToken, Status, CreatedAt) " +
                "VALUES (@AdminId, @Name, @Description, @LinkToken, 'Open', @CreatedAt); " +
                "SELECT SCOPE_IDENTITY();", conn);
            cmd.Parameters.AddWithValue("@AdminId",      adminId);
            cmd.Parameters.AddWithValue("@Name",         name);
            cmd.Parameters.AddWithValue("@Description",  (object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LinkToken",    linkToken);
            cmd.Parameters.AddWithValue("@CreatedAt",    DateTime.UtcNow);
            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task UpdateSessionStatusAsync(int sessionId, string status)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "UPDATE PackingSession SET Status = @Status WHERE SessionId = @SessionId", conn);
            cmd.Parameters.AddWithValue("@Status",    status);
            cmd.Parameters.AddWithValue("@SessionId", sessionId);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteSessionAsync(int sessionId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            // Delete in reverse FK order
            foreach (var sql in new[]
            {
                "DELETE FROM BoxAuditLog    WHERE SessionId = @Id",
                "DELETE FROM SessionBox     WHERE SessionId = @Id",
                "DELETE FROM AccessRequest  WHERE SessionId = @Id",
                "DELETE FROM SessionUser    WHERE SessionId = @Id",
                "DELETE FROM PackingSession WHERE SessionId = @Id"
            })
            {
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", sessionId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // ═══════════════════════════════════════════════════════
        // Session Users
        // ═══════════════════════════════════════════════════════

        public async Task<SessionUser?> GetSessionUserByTokenAsync(string token)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "SELECT SessionUserId, SessionId, DisplayName, Email, Token, CreatedAt " +
                "FROM SessionUser WHERE Token = @Token", conn);
            cmd.Parameters.AddWithValue("@Token", token);
            await conn.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();
            if (!await rdr.ReadAsync()) return null;
            return MapSessionUser(rdr);
        }

        public async Task<int> CreateSessionUserAsync(int sessionId, string displayName, string? email, string token)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "INSERT INTO SessionUser (SessionId, DisplayName, Email, Token, CreatedAt) " +
                "VALUES (@SessionId, @DisplayName, @Email, @Token, @CreatedAt); " +
                "SELECT SCOPE_IDENTITY();", conn);
            cmd.Parameters.AddWithValue("@SessionId",   sessionId);
            cmd.Parameters.AddWithValue("@DisplayName", displayName);
            cmd.Parameters.AddWithValue("@Email",       (object?)email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Token",       token);
            cmd.Parameters.AddWithValue("@CreatedAt",   DateTime.UtcNow);
            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        // ═══════════════════════════════════════════════════════
        // Access Requests
        // ═══════════════════════════════════════════════════════

        public async Task<List<AccessRequest>> GetRequestsBySessionAsync(int sessionId)
        {
            var list = new List<AccessRequest>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "SELECT ar.RequestId, ar.SessionId, ar.SessionUserId, ar.Status, " +
                "       ar.RequestedAt, ar.RespondedAt, su.DisplayName, su.Email " +
                "FROM AccessRequest ar " +
                "JOIN SessionUser su ON su.SessionUserId = ar.SessionUserId " +
                "WHERE ar.SessionId = @SessionId ORDER BY ar.RequestedAt DESC", conn);
            cmd.Parameters.AddWithValue("@SessionId", sessionId);
            await conn.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(MapAccessRequest(rdr));
            return list;
        }

        public async Task<AccessRequest?> GetRequestByIdAsync(int requestId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "SELECT ar.RequestId, ar.SessionId, ar.SessionUserId, ar.Status, " +
                "       ar.RequestedAt, ar.RespondedAt, su.DisplayName, su.Email " +
                "FROM AccessRequest ar " +
                "JOIN SessionUser su ON su.SessionUserId = ar.SessionUserId " +
                "WHERE ar.RequestId = @RequestId", conn);
            cmd.Parameters.AddWithValue("@RequestId", requestId);
            await conn.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();
            if (!await rdr.ReadAsync()) return null;
            return MapAccessRequest(rdr);
        }

        public async Task<AccessRequest?> GetRequestByUserTokenAsync(string userToken)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "SELECT ar.RequestId, ar.SessionId, ar.SessionUserId, ar.Status, " +
                "       ar.RequestedAt, ar.RespondedAt, su.DisplayName, su.Email " +
                "FROM AccessRequest ar " +
                "JOIN SessionUser su ON su.SessionUserId = ar.SessionUserId " +
                "WHERE su.Token = @Token ORDER BY ar.RequestedAt DESC", conn);
            cmd.Parameters.AddWithValue("@Token", userToken);
            await conn.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();
            if (!await rdr.ReadAsync()) return null;
            return MapAccessRequest(rdr);
        }

        public async Task<int> CreateAccessRequestAsync(int sessionId, int sessionUserId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "INSERT INTO AccessRequest (SessionId, SessionUserId, Status, RequestedAt) " +
                "VALUES (@SessionId, @SessionUserId, 'Pending', @RequestedAt); " +
                "SELECT SCOPE_IDENTITY();", conn);
            cmd.Parameters.AddWithValue("@SessionId",     sessionId);
            cmd.Parameters.AddWithValue("@SessionUserId", sessionUserId);
            cmd.Parameters.AddWithValue("@RequestedAt",   DateTime.UtcNow);
            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task UpdateRequestStatusAsync(int requestId, string status)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "UPDATE AccessRequest SET Status = @Status, RespondedAt = @RespondedAt " +
                "WHERE RequestId = @RequestId", conn);
            cmd.Parameters.AddWithValue("@Status",      status);
            cmd.Parameters.AddWithValue("@RespondedAt", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@RequestId",   requestId);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // ═══════════════════════════════════════════════════════
        // Session Boxes
        // ═══════════════════════════════════════════════════════

        public async Task<List<SessionBox>> GetSessionBoxesAsync(int sessionId)
        {
            var list = new List<SessionBox>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "SELECT sb.SessionBoxId, sb.SessionId, sb.BoxId, sb.Quantity, sb.AddedBy, sb.AddedAt, " +
                "       b.BoxName, b.Width, b.Height, b.Depth, b.WeightKg, b.IsFragile, b.AllowRotation " +
                "FROM SessionBox sb " +
                "JOIN Box b ON b.BoxId = sb.BoxId " +
                "WHERE sb.SessionId = @SessionId ORDER BY sb.AddedAt", conn);
            cmd.Parameters.AddWithValue("@SessionId", sessionId);
            await conn.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new SessionBox
                {
                    SessionBoxId = rdr.GetInt32(0),
                    SessionId    = rdr.GetInt32(1),
                    BoxId        = rdr.GetInt32(2),
                    Quantity     = rdr.GetInt32(3),
                    AddedBy      = rdr.GetString(4),
                    AddedAt      = rdr.GetDateTime(5),
                    BoxName      = rdr.GetString(6),
                    Width        = rdr.GetDouble(7),
                    Height       = rdr.GetDouble(8),
                    Depth        = rdr.GetDouble(9),
                    WeightKg     = rdr.GetDouble(10),
                    IsFragile    = rdr.GetBoolean(11),
                    AllowRotation = rdr.GetBoolean(12)
                });
            }
            return list;
        }

        public async Task<int> AddSessionBoxAsync(int sessionId, int boxId, int quantity, string addedBy)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "INSERT INTO SessionBox (SessionId, BoxId, Quantity, AddedBy, AddedAt) " +
                "VALUES (@SessionId, @BoxId, @Quantity, @AddedBy, @AddedAt); " +
                "SELECT SCOPE_IDENTITY();", conn);
            cmd.Parameters.AddWithValue("@SessionId", sessionId);
            cmd.Parameters.AddWithValue("@BoxId",     boxId);
            cmd.Parameters.AddWithValue("@Quantity",  quantity);
            cmd.Parameters.AddWithValue("@AddedBy",   addedBy);
            cmd.Parameters.AddWithValue("@AddedAt",   DateTime.UtcNow);
            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<bool> DeleteSessionBoxAsync(int sessionBoxId, int sessionId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "DELETE FROM SessionBox WHERE SessionBoxId = @SessionBoxId AND SessionId = @SessionId", conn);
            cmd.Parameters.AddWithValue("@SessionBoxId", sessionBoxId);
            cmd.Parameters.AddWithValue("@SessionId",    sessionId);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // ═══════════════════════════════════════════════════════
        // Audit Log
        // ═══════════════════════════════════════════════════════

        public async Task<List<BoxAuditLog>> GetAuditLogAsync(int sessionId)
        {
            var list = new List<BoxAuditLog>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "SELECT LogId, SessionId, Action, BoxId, BoxName, Quantity, ChangedBy, ChangedByType, ChangedAt " +
                "FROM BoxAuditLog WHERE SessionId = @SessionId ORDER BY ChangedAt DESC", conn);
            cmd.Parameters.AddWithValue("@SessionId", sessionId);
            await conn.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new BoxAuditLog
                {
                    LogId         = rdr.GetInt32(0),
                    SessionId     = rdr.GetInt32(1),
                    Action        = rdr.GetString(2),
                    BoxId         = rdr.IsDBNull(3) ? null : rdr.GetInt32(3),
                    BoxName       = rdr.GetString(4),
                    Quantity      = rdr.IsDBNull(5) ? null : rdr.GetInt32(5),
                    ChangedBy     = rdr.GetString(6),
                    ChangedByType = rdr.GetString(7),
                    ChangedAt     = rdr.GetDateTime(8)
                });
            }
            return list;
        }

        public async Task AddAuditLogAsync(BoxAuditLog log)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "INSERT INTO BoxAuditLog " +
                "(SessionId, Action, BoxId, BoxName, Quantity, ChangedBy, ChangedByType, ChangedAt) " +
                "VALUES (@SessionId, @Action, @BoxId, @BoxName, @Quantity, @ChangedBy, @ChangedByType, @ChangedAt)", conn);
            cmd.Parameters.AddWithValue("@SessionId",     log.SessionId);
            cmd.Parameters.AddWithValue("@Action",        log.Action);
            cmd.Parameters.AddWithValue("@BoxId",         (object?)log.BoxId   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BoxName",       log.BoxName);
            cmd.Parameters.AddWithValue("@Quantity",      (object?)log.Quantity ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ChangedBy",     log.ChangedBy);
            cmd.Parameters.AddWithValue("@ChangedByType", log.ChangedByType);
            cmd.Parameters.AddWithValue("@ChangedAt",     log.ChangedAt);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // ═══════════════════════════════════════════════════════
        // Private Mappers
        // ═══════════════════════════════════════════════════════

        private static PackingSession MapSession(SqlDataReader r) => new()
        {
            SessionId   = r.GetInt32(0),
            AdminId     = r.GetInt32(1),
            Name        = r.GetString(2),
            Description = r.IsDBNull(3) ? null : r.GetString(3),
            LinkToken   = r.GetString(4),
            Status      = r.GetString(5),
            CreatedAt   = r.GetDateTime(6)
        };

        private static SessionUser MapSessionUser(SqlDataReader r) => new()
        {
            SessionUserId = r.GetInt32(0),
            SessionId     = r.GetInt32(1),
            DisplayName   = r.GetString(2),
            Email         = r.IsDBNull(3) ? null : r.GetString(3),
            Token         = r.GetString(4),
            CreatedAt     = r.GetDateTime(5)
        };

        private static AccessRequest MapAccessRequest(SqlDataReader r) => new()
        {
            RequestId     = r.GetInt32(0),
            SessionId     = r.GetInt32(1),
            SessionUserId = r.GetInt32(2),
            Status        = r.GetString(3),
            RequestedAt   = r.GetDateTime(4),
            RespondedAt   = r.IsDBNull(5) ? null : r.GetDateTime(5),
            DisplayName   = r.IsDBNull(6) ? null : r.GetString(6),
            Email         = r.IsDBNull(7) ? null : r.GetString(7)
        };
    }
}
