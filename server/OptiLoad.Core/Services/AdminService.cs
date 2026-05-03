using OptiLoad.Core.Models;
using System.Linq;

namespace OptiLoad.Core.Services
{
    public interface IAdminService
    {
        Admin? Authenticate(string username, string password);
    }

    public class AdminService : IAdminService
    {
        private readonly List<Admin> _admins; // Replace with DB context in real app

        public AdminService()
        {
            // Example admin: username=admin, password=admin123 (hashed)
            PasswordHasher.CreatePasswordHash("admin123", out var hash, out var salt);
            _admins = new List<Admin>
            {
                new Admin { Id = 1, Username = "admin", PasswordHash = hash, PasswordSalt = salt }
            };
        }

        public Admin? Authenticate(string username, string password)
        {
            var admin = _admins.FirstOrDefault(a => a.Username == username);
            if (admin == null) return null;
            if (!PasswordHasher.VerifyPassword(password, admin.PasswordHash, admin.PasswordSalt))
                return null;
            return admin;
        }
    }
}
