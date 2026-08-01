using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace KanbanBackend.Models
{
    public class AuthOptions
    {
        public const string ISSUER = "Server";
        public const string AUDIENCE = "Client";
        const string KEY = "very_long_secret_key_for_authorization";
        public static SymmetricSecurityKey GetSymmetricSecurityKey() =>
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(KEY));
    }
}
