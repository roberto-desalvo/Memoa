using FluentResults;
using MediatR;
using Memoa.Domain.Dtos;
using System.ComponentModel.DataAnnotations;

namespace Memoa.Domain.Requests
{
    public class UpdateEntryRequest : IRequest<Result<EntryDto>>
    {
        public Guid EntryId { get; set; }
        
        [Required]
        [MaxLength(100000, ErrorMessage = "Il testo non può superare i 100.000 caratteri")]
        public string Text { get; set; } = string.Empty;
        
        [MaxLength(100000, ErrorMessage = "La trascrizione non può superare i 100.000 caratteri")]
        public string? AudioTranscription { get; set; }

        // Audio replacement fields
        public string? AudioPath { get; set; }
        public string? AudioMime { get; set; }
        public int? AudioDurationMs { get; set; }
        public long? AudioSizeBytes { get; set; }

        public UpdateEntryRequest(Guid entryId)
        {
            EntryId = entryId;
        }

        public UpdateEntryRequest() { }
    }
}
