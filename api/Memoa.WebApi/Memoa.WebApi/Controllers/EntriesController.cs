using FluentResults;
using MediatR;
using Memoa.Core.Api;
using Memoa.Domain.Dtos;
using Memoa.Domain.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoa.WebApi.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/[controller]")]
    public class EntriesController : BaseController
    {
        private readonly IMediator _mediator;

        public EntriesController(IMediator mediator)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        }

        [HttpPost(Name = "CreateEntry")]
        [ProducesResponseType<EntryDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> CreateEntry([FromBody] CreateEntryRequest request)
        {
            var result = await _mediator.Send(request);
            return CreateApiResult(result);
        }

        [HttpGet(Name = "GetEntries")]
        [ProducesResponseType<List<EntryDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEntries()
        {
            var result = await _mediator.Send(new GetEntriesRequest());
            return CreateApiResult(result);
        }

        [HttpDelete("{id}", Name = "DeleteEntry")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteEntry(Guid id)
        {
            var result = await _mediator.Send(new DeleteEntryRequest(id));
            return CreateApiResult(result);
        }
    }
}
