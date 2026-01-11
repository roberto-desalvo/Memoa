using FluentResults;
using MediatR;
using Memoa.Core.Errors;
using Memoa.Application.Services;
using Memoa.Domain.Dtos;
using Memoa.Domain.Repositories;
using Memoa.Domain.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Memoa.Application.Handlers
{
    public class UpdateEntryRequestHandler : IRequestHandler<UpdateEntryRequest, Result<EntryDto>>
    {
        private readonly IHttpContextAccessor _accessor;
        private readonly IEntryRepository _entryRepository;
        private readonly IAudioStorageService _audioStorageService;
        private readonly ILogger<UpdateEntryRequestHandler> _logger;

        public UpdateEntryRequestHandler(
            IHttpContextAccessor accessor,
            IEntryRepository entryRepository,
            IAudioStorageService audioStorageService,
            ILogger<UpdateEntryRequestHandler> logger)
        {
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            _entryRepository = entryRepository ?? throw new ArgumentNullException(nameof(entryRepository));
            _audioStorageService = audioStorageService ?? throw new ArgumentNullException(nameof(audioStorageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<EntryDto>> Handle(UpdateEntryRequest request, CancellationToken cancellationToken)
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
                return UnauthorizedError.ResultFail<EntryDto>("Non sei autorizzato a modificare questa nota");
            }

            // Aggiorna i campi
            entry.Text = request.Text;
            entry.AudioTranscription = request.AudioTranscription;
            entry.UpdatedAt = DateTime.UtcNow;

            // Gestione sostituzione audio
            if (!string.IsNullOrWhiteSpace(request.AudioPath))
            {
                // Elimina il vecchio audio se presente
                if (!string.IsNullOrWhiteSpace(entry.AudioPath))
                {
                    try
                    {
                        await _audioStorageService.DeleteAudioAsync(entry.AudioPath!, cancellationToken);
                        _logger.LogInformation("Vecchio audio eliminato con successo per Entry {EntryId}", request.EntryId);
                    }
                    catch (AudioNotFoundException)
                    {
                        // Audio non trovato (404) - OK, continua con aggiornamento
                        _logger.LogInformation("Vecchio audio non trovato per Entry {EntryId}, procedo con aggiornamento", request.EntryId);
                    }
                    catch (Exception ex)
                    {
                        // Altri errori - registra ma continua (evita blocco se storage ha problemi)
                        _logger.LogWarning(ex, "Eliminazione vecchio audio fallita per Entry {EntryId}, procedo comunque con aggiornamento", request.EntryId);
                    }
                }

                // Aggiorna i metadati audio con il nuovo file
                entry.AudioPath = request.AudioPath;
                entry.AudioMime = request.AudioMime;
                entry.AudioDurationMs = request.AudioDurationMs;
                entry.AudioSizeBytes = request.AudioSizeBytes;
            }

            // Marca l'entry come modificata
            await _entryRepository.UpdateAsync(entry, cancellationToken);

            // Salva le modifiche
            await _entryRepository.SaveChangesAsync(cancellationToken);

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
