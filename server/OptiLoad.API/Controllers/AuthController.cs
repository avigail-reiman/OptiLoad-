using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OptiLoad.Core.Services;
using OptiLoad.API.Services;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace OptiLoad.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAdminService    _adminService;
        private readonly string           _jwtKey;
        private readonly LoginRateLimiter _rateLimiter;

        public AuthController(IAdminService adminService, IConfiguration configuration,
                              LoginRateLimiter rateLimiter)
        {
            _adminService = adminService;
            _jwtKey       = configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT key is not configured.");
            _rateLimiter  = rateLimiter;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var raw = HttpContext.Connection.RemoteIpAddress;
            var ip  = (raw?.IsIPv4MappedToIPv6 == true ? raw.MapToIPv4() : raw)?.ToString() ?? "unknown";

            if (_rateLimiter.IsLockedOut(ip, out int retryAfter))
            {
                Response.Headers["Retry-After"] = retryAfter.ToString();
                return StatusCode(429, $"יותר מדי ניסיונות כושלים. נסה שוב בעוד {retryAfter} שניות.");
            }

            var admin = await _adminService.AuthenticateAsync(req.Username, req.Password);
            if (admin == null)
            {
                _rateLimiter.RecordFailure(ip);
                return Unauthorized("Invalid credentials");
            }

            _rateLimiter.RecordSuccess(ip);

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtKey);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, admin.Username),
                    new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString())
                }),
                Expires = DateTime.UtcNow.AddHours(2),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var jwt = tokenHandler.WriteToken(token);
            return Ok(new { token = jwt });
        }

        public class LoginRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        [Authorize]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { error = "שם משתמש וסיסמא הם שדות חובה" });

            var (success, error) = await _adminService.RegisterAdminAsync(req.Username, req.Password);
            if (!success)
                return BadRequest(new { error });

            return Ok(new { message = $"מנהל '{req.Username}' נוצר בהצלחה" });
        }

        public class RegisterRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }
    }
}
