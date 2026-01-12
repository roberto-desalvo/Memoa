using FluentResults;
using MediatR;
using Memoa.Domain.Dtos;

namespace Memoa.Application.Requests
{
    public class GetEntryByIdRequest : IRequest<Result<EntryDto>>
    {
        public Guid EntryId { get; set; }

        public GetEntryByIdRequest(Guid entryId)
        {
            EntryId = entryId;
        }
    }
}
