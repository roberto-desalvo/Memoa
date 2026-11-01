using System.ComponentModel.DataAnnotations;

namespace Memoa.Functions.Transcription;

/// <summary>
/// Azure AI Speech per la trascrizione (sezione "Speech"). L'autenticazione è sempre Entra ID (la risorsa
/// ha l'autenticazione locale disattivata e un sottodominio personalizzato): in Azure l'identità
/// user-assigned indicata da AZURE_CLIENT_ID, in locale l'utente di Azure CLI o Visual Studio.
/// </summary>
public sealed class SpeechOptions
{
    public const string SectionName = "Speech";

    /// <summary>Endpoint con il sottodominio personalizzato, es. https://&lt;risorsa&gt;.cognitiveservices.azure.com/.</summary>
    [Required]
    public Uri? Endpoint { get; set; }
}
