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

    /// <summary>
    /// Feature flag: se false la Function di trascrizione non chiama Azure AI Speech e inoltra ogni
    /// messaggio al job di trascrizione (coda di ripiego). Impostata da useAzureSpeech in infra/main.bicep.
    /// Questo e gli altri valori sono i predefiniti, sostituibili dalla pagina di amministrazione
    /// (<see cref="Settings.SystemSettingsProvider"/>).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Parte fissa del timeout di una trascrizione, a cui si aggiunge <see cref="TimeoutPerAudioMinuteSeconds"/>
    /// per minuto di audio: Microsoft non garantisce una latenza, ma dichiara circa 15 secondi per 10 minuti.
    /// </summary>
    [Range(1, 600)]
    public int BaseTimeoutSeconds { get; set; } = 30;

    [Range(0, 120)]
    public int TimeoutPerAudioMinuteSeconds { get; set; } = 10;
}
