using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Memoa.Functions.Authentication;

/// <summary>
/// Valida i token di accesso emessi da Entra External ID. Le chiavi di firma e l'issuer arrivano dal
/// documento OpenID Connect del tenant, messo in cache e rinnovato da <see cref="ConfigurationManager{T}"/>.
/// </summary>
public sealed class TokenValidator
{
    private readonly AuthOptions _options;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };
    private readonly ILogger<TokenValidator> _logger;

    public TokenValidator(IOptions<AuthOptions> options, ILogger<TokenValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{_options.Authority.TrimEnd('/')}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });
    }

    public async Task<ClaimsPrincipal?> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        var configuration = await _configurationManager.GetConfigurationAsync(cancellationToken);

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = configuration.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            NameClaimType = "name",
        };

        var result = await _handler.ValidateTokenAsync(token, parameters);

        if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            // Chiavi ruotate dal tenant: si forza il rinnovo dei metadati e si riprova una volta.
            _configurationManager.RequestRefresh();
            configuration = await _configurationManager.GetConfigurationAsync(cancellationToken);
            parameters.IssuerSigningKeys = configuration.SigningKeys;
            result = await _handler.ValidateTokenAsync(token, parameters);
        }

        if (!result.IsValid)
        {
            // Solo il tipo di errore: token e claim non vanno mai nei log.
            _logger.LogInformation("Token rifiutato: {Reason}", result.Exception?.GetType().Name);
            return null;
        }

        if (!HasRequiredScope(result.ClaimsIdentity))
        {
            _logger.LogInformation("Token rifiutato: scope richiesto assente");
            return null;
        }

        return new ClaimsPrincipal(result.ClaimsIdentity);
    }

    private bool HasRequiredScope(ClaimsIdentity identity)
    {
        if (string.IsNullOrEmpty(_options.RequiredScope))
        {
            return true;
        }

        var scopes = identity.FindFirst("scp")?.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return scopes.Contains(_options.RequiredScope, StringComparer.Ordinal);
    }
}
