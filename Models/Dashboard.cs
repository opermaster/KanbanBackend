namespace KanbanBackend.Models
{
    public class Dashboard
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public int UserId { get; set; }
        public User User{ get; set; } = null!;

        public ICollection<BoardMember> Members { get; set; } = new List<BoardMember>();
        public ICollection<BoardInvite> Invites { get; set; } = new List<BoardInvite>();
        public ICollection<Column> Columns { get; set; }      = new List<Column>();
    }
    public class DashboardDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }
}
