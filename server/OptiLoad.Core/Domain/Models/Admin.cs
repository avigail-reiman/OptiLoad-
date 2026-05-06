using System.ComponentModel.DataAnnotations;

namespace OptiLoad.Core.Models
{
    public class Admin
    {
        public int Id { get; set; }
        [Required]
        public string Username { get; set; }
        [Required]
        public string PasswordHash { get; set; }
        [Required]
        public string PasswordSalt { get; set; }
    }
}
