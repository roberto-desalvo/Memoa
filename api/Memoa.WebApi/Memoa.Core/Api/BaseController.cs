using FluentResults;
using Memoa.Core.Errors;
using Memoa.Core.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Net;

namespace Memoa.Core.Api
{
    public class BaseController : ControllerBase
    {
        protected IActionResult CreateApiResult(Result result)
        {
            return CreateApiResult<object>(result);
        }

        protected IActionResult CreateApiResult<T>(Result<T> result)
        {
            if (!result.IsFailed)
            {
                return result.GetSuccessMessages();
            }

            // BadRequest
            if (result.HasError<InvalidInputError>())
            {
                return BadRequest(new ProblemDetails
                {
                    Instance = HttpContext.Request.Path,
                    Title = ReasonPhrases.GetReasonPhrase((int)HttpStatusCode.BadRequest),
                    Status = (int)HttpStatusCode.BadRequest,
                    Type = $"https://httpstatuses.com/{(int)HttpStatusCode.BadRequest}",
                    Detail = result.GetErrorMessages()
                });
            }

            // Unauthorized
            if (result.HasError<UnauthorizedError>())
            {
                return Unauthorized();
            }

            // NotFound
            if (result.HasError<NotFoundError>())
            {
                return NotFound(new ProblemDetails
                {
                    Instance = HttpContext.Request.Path,
                    Title = ReasonPhrases.GetReasonPhrase((int)HttpStatusCode.NotFound),
                    Status = (int)HttpStatusCode.NotFound,
                    Type = $"https://httpstatuses.com/{(int)HttpStatusCode.NotFound}",
                    Detail = result.GetErrorMessages()
                });
            }

            // Empty
            if (result.HasError<EmptyError>())
            {
                return NoContent();
            }

            // UnprocessableEntity
            if (result.HasError<UnprocessableEntityError>())
            {
                return UnprocessableEntity(new ProblemDetails
                {
                    Instance = HttpContext.Request.Path,
                    Title = ReasonPhrases.GetReasonPhrase((int)HttpStatusCode.UnprocessableEntity),
                    Status = (int)HttpStatusCode.UnprocessableEntity,
                    Type = $"https://httpstatuses.com/{(int)HttpStatusCode.UnprocessableEntity}",
                    Detail = result.GetErrorMessages()
                });
            }

            // InternalServerError
            return StatusCode((int)HttpStatusCode.InternalServerError, new ProblemDetails
            {
                Instance = HttpContext.Request.Path,
                Title = ReasonPhrases.GetReasonPhrase((int)HttpStatusCode.UnprocessableEntity),
                Status = (int)HttpStatusCode.UnprocessableEntity,
                Type = $"https://httpstatuses.com/{(int)HttpStatusCode.UnprocessableEntity}",
                Detail = result.GetErrorMessages()
            });
        }
    }
}
