using FluentResults;
using MediatR;
using Memoa.Domain.Dtos;

namespace Memoa.Domain.Requests
{
    public class CreateEntryRequest : IRequest<Result<EntryDto>>
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
