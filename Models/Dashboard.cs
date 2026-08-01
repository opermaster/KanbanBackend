namespace KanbanBackend.Models
{
    public class Dashboard
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public int UserId { get; set; }
        public User User{ get; set; } = null!;
    }
    public class DashboardDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }
}
