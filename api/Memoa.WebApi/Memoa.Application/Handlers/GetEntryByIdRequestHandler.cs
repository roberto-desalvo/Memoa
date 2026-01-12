using FluentResults;
using MediatR;
using Memoa.Core.Errors;
using Memoa.Application.Requests;
using Memoa.Application.Services;
using Memoa.Application.Mapping;
using Memoa.Domain.Dtos;
using Memoa.Domain.Repositories;

namespace Memoa.Application.Handlers
{
    public class GetEntryByIdRequestHandler : IRequestHandler<GetEntryByIdRequest, Result<EntryDto>>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IEntryRepository _entryRepository;

        public GetEntryByIdRequestHandler(
            ICurrentUserService currentUserService,
            IEntryRepository entryRepository)
        {
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _entryRepository = entryRepository ?? throw new ArgumentNullException(nameof(entryRepository));
        }

        public async Task<Result<EntryDto>> Handle(GetEntryByIdRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return UnauthorizedError.ResultFail<EntryDto>("Utente non riconosciuto");
            }

            var entry = await _entryRepository.GetByIdAsync(request.EntryId, cancellationToken);

            if (entry == null)
            {
                return NotFoundError.ResultFail<EntryDto>("Nota non trovata");
            }

            // Usa domain method per verificare ownership
            if (!entry.BelongsToUser(userId))
            {
                return UnauthorizedError.ResultFail<EntryDto>("Non sei autorizzato ad accedere a questa nota");
            }

            return Result.Ok(entry.ToDto());
        }
    }
}
