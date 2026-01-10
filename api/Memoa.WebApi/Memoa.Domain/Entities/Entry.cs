namespace Memoa.Domain.Entities
{
    public class Entry
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
