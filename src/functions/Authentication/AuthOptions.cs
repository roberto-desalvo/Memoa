using System.ComponentModel.DataAnnotations;

namespace Memoa.Functions.Authentication;

/// <summary>Configurazione della validazione dei token di Entra External ID (sezione "Auth").</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Authority del tenant esterno, es. https://&lt;sottodominio&gt;.ciamlogin.com/&lt;tenant-id&gt;/v2.0.</summary>
    [Required]
    public string Authority { get; set; } = string.Empty;

    /// <summary>Client ID della registrazione dell'API (claim "aud" dei token v2).</summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>Scope delegato richiesto nel claim "scp". Vuoto per non controllarlo.</summary>
    public string RequiredScope { get; set; } = "access_as_user";
}
