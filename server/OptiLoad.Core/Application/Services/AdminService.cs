using OptiLoad.Core.Models;

namespace OptiLoad.Core.Services
{
    public interface IAdminService
    {
        Task<Admin?> AuthenticateAsync(string username, string password);
        Task SeedDefaultAdminIfEmptyAsync();
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
    }
}
