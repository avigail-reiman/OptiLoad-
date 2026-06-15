using OptiLoad.Core.Models;

namespace OptiLoad.Core.Services
{
    public interface IAdminService
    {
        Task<Admin?> AuthenticateAsync(string username, string password);
        Task SeedDefaultAdminIfEmptyAsync();
        Task<(bool success, string error)> RegisterAdminAsync(string username, string password);
    }

    public class AdminService : IAdminService
    {
        private readonly IAdminRepository _repo;

        public AdminService(IAdminRepository repo)
        {
            _repo = repo;
        }

        public async Task<Admin?> AuthenticateAsync(string username, string password)
        {
            var admin = await _repo.GetAdminByUsername(username);
            if (admin == null) return null;
            if (!PasswordHasher.VerifyPassword(password, admin.PasswordHash, admin.PasswordSalt))
                return null;
            return admin;
        }

        public async Task SeedDefaultAdminIfEmptyAsync()
        {
            if (await _repo.AdminsExist()) return;
            PasswordHasher.CreatePasswordHash("Admin@1234!", out var hash, out var salt);
            await _repo.CreateAdmin("admin", hash, salt);
        }

        public async Task<(bool success, string error)> RegisterAdminAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
                return (false, "שם משתמש חייב להיות לפחות 3 תווים");
            if (username.Length > 50)
                return (false, "שם משתמש ארוך מדי");
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
                return (false, "סיסמא חייבת להיות לפחות 6 תווים");

            var existing = await _repo.GetAdminByUsername(username);
            if (existing != null)
                return (false, "שם משתמש כבר קיים");

            PasswordHasher.CreatePasswordHash(password, out var hash, out var salt);
            await _repo.CreateAdmin(username.Trim(), hash, salt);
            return (true, string.Empty);
        }
    }
}
