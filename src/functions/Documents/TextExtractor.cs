using System.Text;
using System.Text.RegularExpressions;
using Memoa.Functions.Functions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;
using UglyToad.PdfPig.Fonts;

namespace Memoa.Functions.Documents;

/// <summary>
/// Estrae il testo di un file caricato, riconoscendo il formato reale dal contenuto (non dall'estensione
/// né dal tipo dichiarato dal client): PDF dall'intestazione "%PDF-", altrimenti testo in UTF-8 valido
/// (Markdown e testo semplice si trattano allo stesso modo). Il PDF deve avere un livello di testo: nessun
/// OCR, un PDF scansionato non ha testo.
/// </summary>
public static partial class TextExtractor
{
    // Codici d'errore: dipendono solo dal file, quindi riprovare non serve (vedi IsContentError).
    public const string FormatNotSupported = "format_not_supported";
    public const string PdfEncrypted = "pdf_encrypted";
    public const string NoText = "no_text";
    public const string TextTooLong = "text_too_long";

    /// <summary>Tentativi esauriti per un errore non legato al file (lettura del blob, database).</summary>
    public const string ProcessingFailed = Memoa.Shared.Pipeline.NormalizedAudio.ProcessingFailed;

    private static readonly byte[] PdfHeader = "%PDF-"u8.ToArray();

    // Alcuni PDF hanno qualche byte prima dell'intestazione: lo standard la cerca nel primo kilobyte.
    private const int PdfHeaderSearchBytes = 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>True se l'errore dipende dal file: "riprova" non lo risolverebbe.</summary>
    public static bool IsContentError(string? errorCode) => errorCode is
        ValidateUploadFunction.FileEmpty or ValidateUploadFunction.FileTooLarge
        or FormatNotSupported or PdfEncrypted or NoText or TextTooLong;

    public static DocumentText Extract(BinaryData content)
    {
        var bytes = content.ToMemory().Span;

        if (bytes.IsEmpty)
        {
            return DocumentText.Failed(DocumentFormat.Unknown, ValidateUploadFunction.FileEmpty);
        }

        if (bytes[..Math.Min(bytes.Length, PdfHeaderSearchBytes)].IndexOf(PdfHeader) >= 0)
        {
            return bytes.Length > DocumentLimits.MaxPdfBytes
                ? DocumentText.Failed(DocumentFormat.Pdf, ValidateUploadFunction.FileTooLarge)
                : ExtractPdf(content);
        }

        if (bytes.Length > DocumentLimits.MaxTextFileBytes)
        {
            return DocumentText.Failed(DocumentFormat.Text, ValidateUploadFunction.FileTooLarge);
        }

        // Un file binario (immagine, DOCX, eseguibile) non è UTF-8 valido o contiene byte nulli.
        string text;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return DocumentText.Failed(DocumentFormat.Unknown, FormatNotSupported);
        }

        if (text.Contains('\0'))
        {
            return DocumentText.Failed(DocumentFormat.Unknown, FormatNotSupported);
        }

        // Il BOM non fa parte del testo.
        return Complete(DocumentFormat.Text, text.TrimStart('﻿'), pages: null);
    }

    private static DocumentText ExtractPdf(BinaryData content)
    {
        try
        {
            using var document = PdfDocument.Open(content.ToArray());
            var pages = new List<string>(document.NumberOfPages);

            foreach (var page in document.GetPages())
            {
                // Ordine di lettura ricostruito dalla posizione delle parole, anche su più colonne.
                pages.Add(ContentOrderTextExtractor.GetText(page));
            }

            return Complete(DocumentFormat.Pdf, string.Join("\n\n", pages), document.NumberOfPages);
        }
        catch (PdfDocumentEncryptedException)
        {
            return DocumentText.Failed(DocumentFormat.Pdf, PdfEncrypted);
        }
        catch (Exception ex) when (ex is PdfDocumentFormatException or InvalidFontFormatException or CorruptCompressedDataException)
        {
            // PDF danneggiato o non conforme: riprovare non serve.
            return DocumentText.Failed(DocumentFormat.Pdf, FormatNotSupported);
        }
    }

    private static DocumentText Complete(DocumentFormat format, string text, int? pages)
    {
        var normalized = Normalize(text);

        if (normalized.Length == 0)
        {
            return DocumentText.Failed(format, NoText, pages);
        }

        return normalized.Length > DocumentLimits.MaxTextLength
            ? DocumentText.Failed(format, TextTooLong, pages, normalized.Length)
            : new DocumentText(format, normalized, null, pages, normalized.Length);
    }

    /// <summary>
    /// Fine riga uniformi, spazi in coda alle righe tolti e al più una riga vuota di seguito: i caratteri
    /// contano per il limite, e gli spazi di impaginazione dei PDF non sono testo.
    /// </summary>
    private static string Normalize(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n').Select(l => l.TrimEnd());
        return ExtraBlankLines().Replace(string.Join('\n', lines), "\n\n").Trim();
    }

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExtraBlankLines();
}

public enum DocumentFormat
{
    Unknown,
    Pdf,
    Text,
}

/// <summary>
/// Esito dell'estrazione: il testo, oppure il codice d'errore. <paramref name="Pages"/> vale solo per i
/// PDF; <paramref name="Characters"/> è la lunghezza del testo normalizzato, anche se oltre il limite.
/// </summary>
public sealed record DocumentText(DocumentFormat Format, string? Text, string? ErrorCode, int? Pages, int Characters)
{
    public static DocumentText Failed(DocumentFormat format, string errorCode, int? pages = null, int characters = 0) =>
        new(format, null, errorCode, pages, characters);
}
