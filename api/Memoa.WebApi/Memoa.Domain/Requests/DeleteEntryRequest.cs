using FluentResults;
using MediatR;

namespace Memoa.Domain.Requests
{
    public class DeleteEntryRequest : IRequest<Result>
    {
        public Guid EntryId { get; set; }

        public DeleteEntryRequest(Guid entryId)
        {
            EntryId = entryId;
        }
    }
}
