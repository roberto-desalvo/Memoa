using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Embeddings;
using Memoa.Functions.Transcription;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Settings;

/// <summary>Valori effettivi dei parametri di sistema: quelli salvati nel database o, se null, la configurazione.</summary>
public sealed record EffectiveSystemSettings(
    bool SpeechEnabled,
    TimeSpan SpeechBaseTimeout,
    TimeSpan SpeechTimeoutPerAudioMinute,
    double SearchMinSimilarity,
    int SearchMaxResults,
    double TaggingMinSimilarity,
    TimeSpan TaggingPendingTimeout);

/// <summary>
/// Parametri di sistema per l'invocazione corrente (scoped). La riga di <see cref="SystemSettings"/> si
/// legge al più una volta al minuto per istanza (<see cref="SystemSettingsCache"/>), solo quando serve:
/// nessuna lettura periodica, così il database serverless può andare in pausa. Una modifica dalla pagina di
/// amministrazione vale subito sull'istanza che la salva, sulle altre entro <see cref="SystemSettingsCache.Duration"/>.
/// </summary>
public sealed class SystemSettingsProvider(
    MemoaDbContext db,
    SystemSettingsCache cache,
    IOptions<SpeechOptions> speechOptions,
    IOptions<SearchOptions> searchOptions,
    IOptions<TaggingOptions> taggingOptions)
{
    public async Task<EffectiveSystemSettings> GetAsync(CancellationToken cancellationToken)
    {
        if (!cache.TryGet(out var stored))
        {
            stored = await db.SystemSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == SystemSettings.SingletonId, cancellationToken);
            cache.Set(stored);
        }

        var speech = speechOptions.Value;
        var search = searchOptions.Value;
        var tagging = taggingOptions.Value;

        return new EffectiveSystemSettings(
            stored?.SpeechEnabled ?? speech.Enabled,
            TimeSpan.FromSeconds(stored?.SpeechBaseTimeoutSeconds ?? speech.BaseTimeoutSeconds),
            TimeSpan.FromSeconds(stored?.SpeechTimeoutPerAudioMinuteSeconds ?? speech.TimeoutPerAudioMinuteSeconds),
            stored?.SearchMinSimilarity ?? search.MinSimilarity,
            stored?.SearchMaxResults ?? search.MaxResults,
            stored?.TaggingMinSimilarity ?? tagging.MinSimilarity,
            TimeSpan.FromMinutes(stored?.TaggingPendingTimeoutMinutes ?? tagging.PendingTimeoutMinutes));
    }
}

/// <summary>Ultima riga letta di <see cref="SystemSettings"/> (null se non esiste), per istanza (singleton).</summary>
public sealed class SystemSettingsCache
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(1);

    private sealed record Entry(SystemSettings? Value, DateTime LoadedAt);

    private volatile Entry? entry;

    public bool TryGet(out SystemSettings? value)
    {
        var current = entry;
        value = current?.Value;
        return current is not null && DateTime.UtcNow - current.LoadedAt < Duration;
    }

    public void Set(SystemSettings? value) => entry = new Entry(value, DateTime.UtcNow);

    public void Invalidate() => entry = null;
}
