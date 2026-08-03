namespace KanbanBackend.Models
{
    public class BoardMember
    {
        public int Id { get; set; }
        public int BoardId { get; set; }
        public Dashboard Board { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public BoardRole Role { get; set; }
    }

    public enum BoardRole { Owner, Editor, Viewer }
}
