using FluentResults;
using MediatR;
using Memoa.Core.Errors;
using Memoa.Application.Services;
using Memoa.Domain.Repositories;
using Memoa.Domain.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Memoa.Application.Handlers
{
    public class DeleteEntryRequestHandler : IRequestHandler<DeleteEntryRequest, Result>
    {
        private readonly IHttpContextAccessor _accessor;
        private readonly IEntryRepository _entryRepository;
        private readonly IAudioStorageService _audioStorageService;
        private readonly ILogger<DeleteEntryRequestHandler> _logger;

        public DeleteEntryRequestHandler(
            IHttpContextAccessor accessor,
            IEntryRepository entryRepository,
            IAudioStorageService audioStorageService,
            ILogger<DeleteEntryRequestHandler> logger)
        {
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            _entryRepository = entryRepository ?? throw new ArgumentNullException(nameof(entryRepository));
            _audioStorageService = audioStorageService ?? throw new ArgumentNullException(nameof(audioStorageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result> Handle(DeleteEntryRequest request, CancellationToken cancellationToken)
        {
            // Verifica autenticazione
            var user = _accessor.HttpContext.User;
            var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return UnauthorizedError.ResultFail("Utente non riconosciuto");
            }

            // Recupera l'entry per verificare che appartenga all'utente
            var entry = await _entryRepository.GetByIdAsync(request.EntryId, cancellationToken);

            if (entry == null)
            {
                return NotFoundError.ResultFail("Nota non trovata");
            }

            // Verifica che l'entry appartenga all'utente
            if (entry.UserId != userId)
            {
                return UnauthorizedError.ResultFail("Non sei autorizzato a eliminare questa nota");
            }

            // Elimina l'audio PRIMA dell'entry (se presente)
            if (!string.IsNullOrWhiteSpace(entry.AudioPath))
            {
                try
                {
                    await _audioStorageService.DeleteAudioAsync(entry.AudioPath!, cancellationToken);
                    _logger.LogInformation("Audio eliminato con successo per Entry {EntryId}", request.EntryId);
                }
                catch (AudioNotFoundException)
                {
                    // Audio non trovato (404) - OK, continua con eliminazione entry
                    _logger.LogInformation("Audio non trovato per Entry {EntryId}, procedo con eliminazione entry", request.EntryId);
                }
                catch (Exception ex)
                {
                    // Altri errori - NON eliminare l'entry per evitare file orfani
                    _logger.LogError(ex, "Eliminazione audio fallita per Entry {EntryId} (path: {AudioPath}). Entry NON eliminata per evitare file orfani.",
                        request.EntryId, entry.AudioPath);
                    return AudioStorageError.ResultFail($"Impossibile eliminare l'audio associato alla nota: {ex.Message}");
                }
            }

            // Elimina l'entry dal database
            await _entryRepository.DeleteAsync(request.EntryId, cancellationToken);
            await _entryRepository.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }
    }
}
