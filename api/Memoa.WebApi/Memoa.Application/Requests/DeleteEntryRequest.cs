using FluentResults;
using MediatR;

namespace Memoa.Application.Requests
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
