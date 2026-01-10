using FluentResults;
using MediatR;
using Memoa.Core.Errors;
using Memoa.Domain.Repositories;
using Memoa.Domain.Requests;
using Microsoft.AspNetCore.Http;

namespace Memoa.Application.Handlers
{
    public class DeleteEntryRequestHandler : IRequestHandler<DeleteEntryRequest, Result>
    {
        private readonly IHttpContextAccessor _accessor;
        private readonly IEntryRepository _entryRepository;

        public DeleteEntryRequestHandler(IHttpContextAccessor accessor, IEntryRepository entryRepository)
        {
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            _entryRepository = entryRepository ?? throw new ArgumentNullException(nameof(entryRepository));
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

            // Elimina l'entry
            await _entryRepository.DeleteAsync(request.EntryId, cancellationToken);
            await _entryRepository.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }
    }
}
