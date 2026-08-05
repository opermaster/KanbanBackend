namespace KanbanBackend.Models
{
    public class Column
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int Order { get; set; }

        public int DashboardId { get; set; }
        public Dashboard Dashboard { get; set; } = null!;

        public ICollection<Card> Cards{ get; set; } = new List<Card>();
    }
}
