using FluentResults;
using MediatR;
using Memoa.Domain.Dtos;

namespace Memoa.Application.Requests
{
    public class GetUserRequest : IRequest<Result<UserDto>>
    {
    }
}
