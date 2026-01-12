using FluentResults;
using MediatR;
using Memoa.Domain.Dtos;
using Memoa.Domain.Entities;
using Memoa.Domain.Repositories;
using Memoa.Application.Requests;
using Memoa.Application.Services;
using Memoa.Application.Mapping;
using Memoa.Core.Errors;

namespace Memoa.Application.Handlers
{
    public class CreateEntryRequestHandler : IRequestHandler<CreateEntryRequest, Result<EntryDto>>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IEntryRepository _entryRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateEntryRequestHandler(
            ICurrentUserService currentUserService,
            IEntryRepository entryRepository,
            IUnitOfWork unitOfWork)
        {
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _entryRepository = entryRepository ?? throw new ArgumentNullException(nameof(entryRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<Result<EntryDto>> Handle(CreateEntryRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return UnauthorizedError.ResultFail<EntryDto>("Utente non riconosciuto");
            }

            // Usa il factory method del domain model
            var entryResult = Entry.Create(
                request.Id,
                userId,
                request.Title,
                request.Text,
                request.CreatedAt,
                request.AudioPath,
                request.AudioMime,
                request.AudioDurationMs,
                request.AudioSizeBytes);

            if (entryResult.IsFailed)
            {
                return InvalidInputError.ResultFail<EntryDto>(entryResult.Errors.First().Message);
            }

            var entry = entryResult.Value;

            await _entryRepository.AddAsync(entry, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Ok(entry.ToDto());
        }
    }
}
