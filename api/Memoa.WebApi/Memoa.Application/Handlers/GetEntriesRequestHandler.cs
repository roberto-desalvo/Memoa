using FluentResults;
using MediatR;
using Memoa.Domain.Dtos;
using Memoa.Domain.Repositories;
using Memoa.Domain.Requests;
using Memoa.Core.Errors;
using Microsoft.AspNetCore.Http;

namespace Memoa.Application.Handlers
{
    public class GetEntriesRequestHandler : IRequestHandler<GetEntriesRequest, Result<List<EntryDto>>>
    {
        private readonly IHttpContextAccessor _accessor;
        private readonly IEntryRepository _entryRepository;

        public GetEntriesRequestHandler(IHttpContextAccessor accessor, IEntryRepository entryRepository)
        {
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            _entryRepository = entryRepository ?? throw new ArgumentNullException(nameof(entryRepository));
        }

        public async Task<Result<List<EntryDto>>> Handle(GetEntriesRequest request, CancellationToken cancellationToken)
        {
            var user = _accessor.HttpContext.User;
            var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return UnauthorizedError.ResultFail<List<EntryDto>>("Utente non riconosciuto");
            }

            // Recupera dal repository le entries per questo userId
            var entries = await _entryRepository.GetByUserIdAsync(userId, cancellationToken);

            var entryDtos = entries
                .Select(e => new EntryDto
                {
                    Id = e.Id,
                    Text = e.Text,
                    CreatedAt = e.CreatedAt,
                    AudioPath = e.AudioPath,
                    AudioMime = e.AudioMime,
                    AudioDurationMs = e.AudioDurationMs,
                    AudioSizeBytes = e.AudioSizeBytes
                })
                .ToList();

            return Result.Ok(entryDtos);
        }
    }
}
