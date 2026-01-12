using FluentResults;
using MediatR;
using Memoa.Application.Requests;
using Memoa.Application.Services;
using Memoa.Application.Mapping;
using Memoa.Domain.Dtos;
using Memoa.Domain.Repositories;
using Memoa.Core.Errors;

namespace Memoa.Application.Handlers
{
    public class GetEntriesRequestHandler : IRequestHandler<GetEntriesRequest, Result<List<EntryDto>>>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IEntryRepository _entryRepository;

        public GetEntriesRequestHandler(
            ICurrentUserService currentUserService,
            IEntryRepository entryRepository)
        {
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _entryRepository = entryRepository ?? throw new ArgumentNullException(nameof(entryRepository));
        }

        public async Task<Result<List<EntryDto>>> Handle(GetEntriesRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return UnauthorizedError.ResultFail<List<EntryDto>>("Utente non riconosciuto");
            }

            var entries = await _entryRepository.GetByUserIdAsync(userId, cancellationToken);

            return Result.Ok(entries.ToDtoList());
        }
    }
}
