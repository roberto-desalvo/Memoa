using FluentResults;
using MediatR;
using Memoa.Core.Errors;
using Memoa.Application.Services;
using Memoa.Application.Requests;
using Memoa.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Memoa.Application.Handlers
{
    public class DeleteEntryRequestHandler : IRequestHandler<DeleteEntryRequest, Result>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IEntryRepository _entryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAudioStorageService _audioStorageService;
        private readonly ILogger<DeleteEntryRequestHandler> _logger;

        public DeleteEntryRequestHandler(
            ICurrentUserService currentUserService,
            IEntryRepository entryRepository,
            IUnitOfWork unitOfWork,
            IAudioStorageService audioStorageService,
            ILogger<DeleteEntryRequestHandler> logger)
        {
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _entryRepository = entryRepository ?? throw new ArgumentNullException(nameof(entryRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _audioStorageService = audioStorageService ?? throw new ArgumentNullException(nameof(audioStorageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result> Handle(DeleteEntryRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return UnauthorizedError.ResultFail("Utente non riconosciuto");
            }

            var entry = await _entryRepository.GetByIdAsync(request.EntryId, cancellationToken);

            if (entry == null)
            {
                return NotFoundError.ResultFail("Nota non trovata");
            }

            // Usa domain method per verificare ownership
            if (!entry.BelongsToUser(userId))
            {
                return UnauthorizedError.ResultFail("Non sei autorizzato a eliminare questa nota");
            }

            // Elimina l'audio PRIMA dell'entry (se presente) usando domain method
            if (entry.HasAudio())
            {
                var audioPath = entry.RemoveAudio();
                
                if (!string.IsNullOrWhiteSpace(audioPath))
                {
                    try
                    {
                        await _audioStorageService.DeleteAudioAsync(audioPath, cancellationToken);
                        _logger.LogInformation("Audio eliminato con successo per Entry {EntryId}", request.EntryId);
                    }
                    catch (AudioNotFoundException)
                    {
                        _logger.LogInformation("Audio non trovato per Entry {EntryId}, procedo con eliminazione entry", request.EntryId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Eliminazione audio fallita per Entry {EntryId} (path: {AudioPath}). Entry NON eliminata per evitare file orfani.",
                            request.EntryId, audioPath);
                        return AudioStorageError.ResultFail($"Impossibile eliminare l'audio associato alla nota: {ex.Message}");
                    }
                }
            }

            await _entryRepository.DeleteAsync(request.EntryId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }
    }
}
