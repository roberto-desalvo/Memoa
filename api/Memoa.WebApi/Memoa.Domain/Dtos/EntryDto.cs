namespace Memoa.Domain.Dtos
{
    public class EntryDto
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? AudioPath { get; set; }
        public string? AudioMime { get; set; }
        public int? AudioDurationMs { get; set; }
        public long? AudioSizeBytes { get; set; }
    }
}
