using FluentResults;
using MediatR;
using Memoa.Core.Errors;
using Memoa.Application.Services;
using Memoa.Application.Requests;
using Memoa.Application.Mapping;
using Memoa.Domain.Dtos;
using Memoa.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Memoa.Application.Handlers
{
    public class UpdateEntryRequestHandler : IRequestHandler<UpdateEntryRequest, Result<EntryDto>>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IEntryRepository _entryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAudioStorageService _audioStorageService;
        private readonly ILogger<UpdateEntryRequestHandler> _logger;

        public UpdateEntryRequestHandler(
            ICurrentUserService currentUserService,
            IEntryRepository entryRepository,
            IUnitOfWork unitOfWork,
            IAudioStorageService audioStorageService,
            ILogger<UpdateEntryRequestHandler> logger)
        {
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _entryRepository = entryRepository ?? throw new ArgumentNullException(nameof(entryRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _audioStorageService = audioStorageService ?? throw new ArgumentNullException(nameof(audioStorageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<EntryDto>> Handle(UpdateEntryRequest request, CancellationToken cancellationToken)
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
                return UnauthorizedError.ResultFail<EntryDto>("Non sei autorizzato a modificare questa nota");
            }

            // Usa domain method para aggiornare il testo
            var updateResult = entry.UpdateText(request.Title, request.Text, request.AudioTranscription);
            if (updateResult.IsFailed)
            {
                return InvalidInputError.ResultFail<EntryDto>(updateResult.Errors.First().Message);
            }

            // Gestione sostituzione audio con domain method
            if (!string.IsNullOrWhiteSpace(request.AudioPath))
            {
                var replaceResult = entry.ReplaceAudio(
                    request.AudioPath,
                    request.AudioMime,
                    request.AudioDurationMs,
                    request.AudioSizeBytes);

                if (replaceResult.IsFailed)
                {
                    return InvalidInputError.ResultFail<EntryDto>(replaceResult.Errors.First().Message);
                }

                var oldAudioPath = replaceResult.Value;

                // Elimina il vecchio audio se presente
                if (!string.IsNullOrWhiteSpace(oldAudioPath))
                {
                    try
                    {
                        await _audioStorageService.DeleteAudioAsync(oldAudioPath, cancellationToken);
                        _logger.LogInformation("Vecchio audio eliminato con successo per Entry {EntryId}", request.EntryId);
                    }
                    catch (AudioNotFoundException)
                    {
                        _logger.LogInformation("Vecchio audio non trovato per Entry {EntryId}, procedo con aggiornamento", request.EntryId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Eliminazione vecchio audio fallita per Entry {EntryId}, procedo comunque con aggiornamento", request.EntryId);
                    }
                }
            }

            await _entryRepository.UpdateAsync(entry, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Ok(entry.ToDto());
        }
    }
}
