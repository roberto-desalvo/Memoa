using Memoa.Functions.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Memoa.Functions.Functions;

public sealed class MeFunction
{
    [Function("Me")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequest req) =>
        new OkObjectResult(new { userId = req.HttpContext.User.GetUserId() });
}
