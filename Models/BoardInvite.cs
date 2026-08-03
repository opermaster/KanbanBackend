namespace KanbanBackend.Models
{
    public class BoardInvite
    {
        public int Id { get; set; }
        public int BoardId { get; set; }

        public Dashboard Board { get; set; } = null!;

        public int InvitedUserId { get; set; }
        public User InvitedUser { get; set; } = null!;

        public int InvitedByUserId { get; set; }

        public InviteStatus Status { get; set; } = InviteStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum InviteStatus { Pending, Accepted, Declined }
}
