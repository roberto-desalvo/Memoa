namespace Memoa.Domain.Dtos
{
    public class EntryDto
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
