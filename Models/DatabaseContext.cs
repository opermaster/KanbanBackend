using Microsoft.EntityFrameworkCore;

namespace KanbanBackend.Models
{
    public class DatabaseContext : DbContext
    {
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Dashboard> Dashboards { get; set; } = null!;
        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) {
            Database.EnsureCreated();
        }
    }
}
