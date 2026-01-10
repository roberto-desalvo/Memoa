using FluentResults;
using MediatR;
using Memoa.Domain.Dtos;

namespace Memoa.Domain.Requests
{
    public class GetUserRequest : IRequest<Result<UserDto>>
    {
    }
}
