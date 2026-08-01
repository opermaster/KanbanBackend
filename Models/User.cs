namespace KanbanBackend.Models
{
    public class User
    {
        public int Id { get; set; }

        public string Login { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public ICollection<Dashboard> Dashboards { get; set; } = new List<Dashboard>();

    }
    public class UserDto
    {
        public int? Id { get; set; } = null!;
        public string Login { get; set; } = null!;
        public string? Password { get; set; } = null!; // !! to make passoword required if needed 

        public User ToUser() {
            return new User() {
                Login = Login,
                PasswordHash = HashPassword(Password),
            };
        }
        private static string HashPassword(string password) {

            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(password);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public static bool VerifyPassword(string password, string storedHash) {
            var hash = HashPassword(password);
            return hash == storedHash;
        }
    }
}
