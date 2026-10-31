namespace Memoa.Functions.Embeddings;

/// <summary>
/// Divide il testo di un record in frammenti da indicizzare. text-embedding-3-small accetta al massimo
/// 8.191 token per input: 2.000 caratteri sono circa 500-700 token, ben sotto il limite, e frammenti
/// brevi rendono la ricerca più precisa. I frammenti si sovrappongono di circa 200 caratteri, così una
/// frase a cavallo di due frammenti resta trovabile.
/// </summary>
public static class TextChunker
{
    public const int MaxChunkLength = 2000;
    public const int OverlapLength = 200;

    // Separatori in ordine di preferenza: paragrafo, riga, fine frase, spazio.
    private static readonly string[][] Breaks =
    [
        ["\n\n", "\r\n\r\n"],
        ["\n"],
        [". ", "! ", "? ", "… ", ".\n", "!\n", "?\n"],
        [" ", "\t"],
    ];

    public static IReadOnlyList<string> Split(string text)
    {
        text = text.Trim();

        var chunks = new List<string>();
        var start = 0;

        while (text.Length - start > MaxChunkLength)
        {
            var end = FindEnd(text, start);
            AddChunk(chunks, text[start..end]);
            start = FindNextStart(text, end);
        }

        AddChunk(chunks, text[start..]);
        return chunks;
    }

    /// <summary>
    /// Fine del frammento che inizia in <paramref name="start"/>: il separatore migliore nella seconda
    /// metà della finestra, altrimenti un taglio netto al limite.
    /// </summary>
    private static int FindEnd(string text, int start)
    {
        var limit = start + MaxChunkLength;
        var minEnd = start + MaxChunkLength / 2;

        foreach (var separators in Breaks)
        {
            var best = -1;
            foreach (var separator in separators)
            {
                // Cerca tra minEnd e limit; il separatore resta nel frammento che chiude.
                var index = text.LastIndexOf(separator, limit - 1, limit - minEnd, StringComparison.Ordinal);
                if (index >= 0)
                {
                    best = Math.Max(best, index + separator.Length);
                }
            }

            if (best > 0)
            {
                return best;
            }
        }

        // Nessun separatore: non spezza una coppia surrogata.
        return char.IsHighSurrogate(text[limit - 1]) ? limit - 1 : limit;
    }

    /// <summary>
    /// Inizio del frammento successivo: circa <see cref="OverlapLength"/> caratteri prima della fine,
    /// all'inizio di una parola. Sempre oltre metà del frammento precedente, quindi il ciclo avanza.
    /// </summary>
    private static int FindNextStart(string text, int end)
    {
        for (var i = end - OverlapLength; i < end; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i + 1;
            }
        }

        return end;
    }

    private static void AddChunk(List<string> chunks, string chunk)
    {
        chunk = chunk.Trim();
        if (chunk.Length > 0)
        {
            chunks.Add(chunk);
        }
    }
}
