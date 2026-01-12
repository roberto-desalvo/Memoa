using FluentResults;
using MediatR;
using Memoa.Domain.Dtos;
using System.ComponentModel.DataAnnotations;

namespace Memoa.Application.Requests
{
    public class CreateEntryRequest : IRequest<Result<EntryDto>>
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(200, ErrorMessage = "Il titolo non può superare i 200 caratteri")]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        public string Text { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; }
        public string? AudioPath { get; set; }
        public string? AudioMime { get; set; }
        public int? AudioDurationMs { get; set; }
        public long? AudioSizeBytes { get; set; }
    }
}
