using FluentResults;
using MediatR;
using Memoa.Application.Requests;
using Memoa.Application.Services;
using Memoa.Domain.Dtos;
using Memoa.Core.Errors;
using Microsoft.AspNetCore.Http;

namespace Memoa.Application.Handlers
{
    public class GetUserRequestHandler : IRequestHandler<GetUserRequest, Result<UserDto>>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IHttpContextAccessor _accessor;

        public GetUserRequestHandler(
            ICurrentUserService currentUserService,
            IHttpContextAccessor accessor)
        {
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
        }

        public async Task<Result<UserDto>> Handle(GetUserRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return UnauthorizedError.ResultFail<UserDto>("Utente non riconosciuto");
            }

            // Email ancora dipende da HttpContext ma è un claim specifico dell'utente
            var user = _accessor.HttpContext.User;
            var email = user.FindFirst("email")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

            var userDto = new UserDto 
            { 
                Id = userId, 
                Email = email ?? string.Empty 
            };

            return Result.Ok(userDto);
        }
    }
}
