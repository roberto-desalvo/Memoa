using FluentResults;
using MediatR;
using Memoa.Domain.Dtos;
using Memoa.Domain.Requests;
using Memoa.Core.Errors;
using Microsoft.AspNetCore.Http;

namespace Memoa.Application.Handlers
{
    public class GetUserRequestHandler : IRequestHandler<GetUserRequest, Result<UserDto>>
    {
        private readonly IHttpContextAccessor _accessor;
        public GetUserRequestHandler(IHttpContextAccessor accessor)
        {
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
        }
        public async Task<Result<UserDto>> Handle(GetUserRequest request, CancellationToken cancellationToken)
        {
            var user = _accessor.HttpContext.User;
            var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var email = user.FindFirst("email")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return UnauthorizedError.ResultFail<UserDto>("Utente non riconosciuto");
            }

            var userDto = new UserDto { Id = userId, Email = email ?? string.Empty };

            return Result.Ok(userDto);
        }

    }
}
