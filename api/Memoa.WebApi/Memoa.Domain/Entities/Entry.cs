namespace Memoa.Domain.Entities
{
    public class Entry
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? AudioPath { get; set; }
        public string? AudioMime { get; set; }
        public int? AudioDurationMs { get; set; }
        public long? AudioSizeBytes { get; set; }
        public string? AudioTranscription { get; set; }
    }
}
