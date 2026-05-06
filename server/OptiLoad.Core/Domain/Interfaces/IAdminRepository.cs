using OptiLoad.Core.Models;

namespace OptiLoad.Core.Services
{
    public interface IAdminRepository
    {
        Task<Admin?> GetAdminByUsername(string username);
        Task<bool>   AdminsExist();
        Task         CreateAdmin(string username, string passwordHash, string passwordSalt);
    }
}
