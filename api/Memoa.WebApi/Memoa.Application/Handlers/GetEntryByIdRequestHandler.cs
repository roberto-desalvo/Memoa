using FluentResults;
using MediatR;
using Memoa.Core.Errors;
using Memoa.Domain.Dtos;
using Memoa.Domain.Repositories;
using Memoa.Domain.Requests;
using Microsoft.AspNetCore.Http;

namespace Memoa.Application.Handlers
{
    public class GetEntryByIdRequestHandler : IRequestHandler<GetEntryByIdRequest, Result<EntryDto>>
    {
        private readonly IHttpContextAccessor _accessor;
        private readonly IEntryRepository _entryRepository;

        public GetEntryByIdRequestHandler(IHttpContextAccessor accessor, IEntryRepository entryRepository)
        {
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            _entryRepository = entryRepository ?? throw new ArgumentNullException(nameof(entryRepository));
        }

        public async Task<Result<EntryDto>> Handle(GetEntryByIdRequest request, CancellationToken cancellationToken)
        {
            // Verifica autenticazione
            var user = _accessor.HttpContext.User;
            var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return UnauthorizedError.ResultFail<EntryDto>("Utente non riconosciuto");
            }

            // Recupera l'entry
            var entry = await _entryRepository.GetByIdAsync(request.EntryId, cancellationToken);

            if (entry == null)
            {
                return NotFoundError.ResultFail<EntryDto>("Nota non trovata");
            }

            // Verifica che l'entry appartenga all'utente
            if (entry.UserId != userId)
            {
                return UnauthorizedError.ResultFail<EntryDto>("Non sei autorizzato ad accedere a questa nota");
            }

            var entryDto = new EntryDto
            {
                Id = entry.Id,
                Text = entry.Text,
                CreatedAt = entry.CreatedAt,
                UpdatedAt = entry.UpdatedAt,
                AudioPath = entry.AudioPath,
                AudioMime = entry.AudioMime,
                AudioDurationMs = entry.AudioDurationMs,
                AudioSizeBytes = entry.AudioSizeBytes,
                AudioTranscription = entry.AudioTranscription
            };

            return Result.Ok(entryDto);
        }
    }
}
