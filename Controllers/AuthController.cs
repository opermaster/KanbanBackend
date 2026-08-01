using KanbanBackend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace KanbanBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly DatabaseContext _context;
        public AuthController(DatabaseContext context) {
            _context = context;
        }
        [HttpPost("new_user")]
        public async Task<ActionResult> Register(UserDto _user, CancellationToken ct) {

            if (await _context.Users.AnyAsync(u => u.Login == _user.Login, ct)) 
                return Conflict("User with this login already exists");

            User user = _user.ToUser();
            _context.Users.Add(user);
            await _context.SaveChangesAsync(ct);

            return StatusCode(201, new { id = user.Id });
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login(UserDto _user, CancellationToken ct) {

            User? user = await _context.Users.FirstOrDefaultAsync(u => u.Login == _user.Login, ct);

            if (user is null || !UserDto.VerifyPassword(_user.Password, user.PasswordHash) ) 
                return Unauthorized("Invalid login or password");

            var claims = new List<Claim> {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Login),
            };
            var jwt = new JwtSecurityToken(
                    issuer: AuthOptions.ISSUER,
                    audience: AuthOptions.AUDIENCE,
                    claims: claims,
                    expires: DateTime.UtcNow.Add(TimeSpan.FromMinutes(30)),
                    signingCredentials: new SigningCredentials(AuthOptions.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256));
            string token = new JwtSecurityTokenHandler().WriteToken(jwt);

            return Ok(new { Token = token });

        }
    }
}
