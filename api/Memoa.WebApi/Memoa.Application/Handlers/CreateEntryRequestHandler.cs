using FluentResults;
using MediatR;
using Memoa.Domain.Dtos;
using Memoa.Domain.Entities;
using Memoa.Domain.Repositories;
using Memoa.Domain.Requests;
using Memoa.Core.Errors;
using Microsoft.AspNetCore.Http;

namespace Memoa.Application.Handlers
{
    public class CreateEntryRequestHandler : IRequestHandler<CreateEntryRequest, Result<EntryDto>>
    {
        private readonly IHttpContextAccessor _accessor;
        private readonly IEntryRepository _entryRepository;

        public CreateEntryRequestHandler(IHttpContextAccessor accessor, IEntryRepository entryRepository)
        {
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            _entryRepository = entryRepository ?? throw new ArgumentNullException(nameof(entryRepository));
        }

        public async Task<Result<EntryDto>> Handle(CreateEntryRequest request, CancellationToken cancellationToken)
        {
            var user = _accessor.HttpContext.User;
            var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return UnauthorizedError.ResultFail<EntryDto>("Utente non riconosciuto");
            }

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return InvalidInputError.ResultFail<EntryDto>("Il testo è obbligatorio");
            }

            // Crea l'entry tramite il repository
            var entry = new Entry
            {
                Id = request.Id,
                UserId = userId,
                Text = request.Text,
                CreatedAt = request.CreatedAt,
                AudioPath = request.AudioPath,
                AudioMime = request.AudioMime,
                AudioDurationMs = request.AudioDurationMs,
                AudioSizeBytes = request.AudioSizeBytes
            };

            await _entryRepository.AddAsync(entry, cancellationToken);
            await _entryRepository.SaveChangesAsync(cancellationToken);

            var entryDto = new EntryDto
            {
                Id = entry.Id,
                Text = entry.Text,
                CreatedAt = entry.CreatedAt,
                AudioPath = entry.AudioPath,
                AudioMime = entry.AudioMime,
                AudioDurationMs = entry.AudioDurationMs,
                AudioSizeBytes = entry.AudioSizeBytes
            };

            return Result.Ok(entryDto);
        }
    }
}
