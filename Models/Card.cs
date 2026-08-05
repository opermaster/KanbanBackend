namespace KanbanBackend.Models
{
    public class Card
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int Order { get; set; }
        public bool Done { get; set; }

        public int ColumnId { get; set; }
        public Column Column{ get; set; } = null!;
    }
}
