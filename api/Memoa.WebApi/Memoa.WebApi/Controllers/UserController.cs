using FluentResults;
using MediatR;
using Memoa.Core.Api;
using Memoa.Domain.Dtos;
using Memoa.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoa.WebApi.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/[controller]")]
    public class UserController : BaseController
    {
        public UserController(IMediator mediator) : base(mediator)
        {
        }

        [HttpGet(Name = "GetUser")]
        [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUser()
        {
            var user = await _mediator.Send(new GetUserRequest());
            return CreateApiResult(user);
        }        
    }
}
