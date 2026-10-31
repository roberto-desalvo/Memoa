using System.ComponentModel.DataAnnotations;

namespace Memoa.Functions.Embeddings;

/// <summary>
/// Azure OpenAI per gli embedding (sezione "OpenAI"). L'autenticazione è sempre Entra ID (l'account ha
/// l'autenticazione locale disattivata): in Azure l'identità user-assigned indicata da AZURE_CLIENT_ID,
/// in locale l'utente di Azure CLI o Visual Studio.
/// </summary>
public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    /// <summary>Es. https://&lt;account&gt;.openai.azure.com/.</summary>
    [Required]
    public Uri? Endpoint { get; set; }

    /// <summary>Nome del deployment di text-embedding-3-small (Data Zone Standard EU).</summary>
    [Required]
    public string EmbeddingDeployment { get; set; } = string.Empty;
}
