using Memoa.Domain.Dtos;
using Memoa.Domain.Entities;

namespace Memoa.Application.Mapping
{
    /// <summary>
    /// Extension methods per mapping tra Entity e DTO
    /// </summary>
    public static class EntryMappingExtensions
    {
        public static EntryDto ToDto(this Entry entry)
        {
            return new EntryDto
            {
                Id = entry.Id,
                Title = entry.Title,
                Text = entry.Text,
                CreatedAt = entry.CreatedAt,
                UpdatedAt = entry.UpdatedAt,
                AudioPath = entry.AudioPath,
                AudioMime = entry.AudioMime,
                AudioDurationMs = entry.AudioDurationMs,
                AudioSizeBytes = entry.AudioSizeBytes,
                AudioTranscription = entry.AudioTranscription
            };
        }

        public static List<EntryDto> ToDtoList(this List<Entry> entries)
        {
            return entries.Select(e => e.ToDto()).ToList();
        }
    }
}
