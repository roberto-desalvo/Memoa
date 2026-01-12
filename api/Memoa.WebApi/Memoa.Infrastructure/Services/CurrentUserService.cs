using Memoa.Application.Services;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Memoa.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public string? GetCurrentUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            
            if (user == null || !user.Identity?.IsAuthenticated == true)
            {
                return null;
            }

            // Prova prima "sub" (standard OIDC/JWT), poi NameIdentifier (ASP.NET default)
            return user.FindFirst("sub")?.Value 
                   ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        public bool IsAuthenticated()
        {
            return _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
        }
    }
}
