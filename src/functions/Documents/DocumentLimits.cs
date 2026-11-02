namespace Memoa.Functions.Documents;

/// <summary>
/// Limiti dei file di testo caricati (PDF, Markdown, testo semplice): circa due pagine A4, cioè circa
/// 6.000 caratteri, con un margine. Le note scritte nell'app hanno un limite diverso
/// (<see cref="Functions.RecordInput.MaxTextLength"/>).
/// </summary>
public static class DocumentLimits
{
    /// <summary>8.000 caratteri in UTF-8, con lettere accentate e marcatori Markdown.</summary>
    public const long MaxTextFileBytes = 16 * 1024;

    /// <summary>Due pagine con font incorporati e qualche immagine (es. un logo).</summary>
    public const long MaxPdfBytes = 1024 * 1024;

    /// <summary>Testo estratto: circa due pagine A4 da 3.000 caratteri, con un margine.</summary>
    public const int MaxTextLength = 8_000;

    public const string PdfContentType = "application/pdf";
    public const string MarkdownContentType = "text/markdown";
    public const string PlainTextContentType = "text/plain";

    /// <summary>
    /// Dimensione massima per il tipo dichiarato dal client, oppure null se il tipo non è ammesso. Il tipo
    /// dichiarato serve solo a scegliere il limite: il formato reale lo verifica <see cref="TextExtractor"/>.
    /// </summary>
    public static long? MaxSizeBytes(string? contentType) => MediaType(contentType) switch
    {
        PdfContentType => MaxPdfBytes,
        MarkdownContentType or PlainTextContentType => MaxTextFileBytes,
        _ => null,
    };

    /// <summary>Il tipo senza parametri (es. "; charset=utf-8"), in minuscolo.</summary>
    public static string MediaType(string? contentType) =>
        (contentType ?? string.Empty).Split(';', 2)[0].Trim().ToLowerInvariant();

    /// <summary>Estensione per il nome del file scaricato, dal tipo salvato con l'allegato.</summary>
    public static string Extension(string? contentType) => MediaType(contentType) switch
    {
        PdfContentType => ".pdf",
        MarkdownContentType => ".md",
        _ => ".txt",
    };
}
