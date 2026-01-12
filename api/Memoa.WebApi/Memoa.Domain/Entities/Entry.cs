using FluentResults;

namespace Memoa.Domain.Entities
{
    /// <summary>
    /// Entità Entry che rappresenta una nota/memo con testo e audio opzionale
    /// </summary>
    public class Entry
    {
        // Properties - uso setter privati per incapsulamento
        public Guid Id { get; private set; }
        public string UserId { get; private set; } = string.Empty;
        public string Text { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        
        // Audio properties
        public string? AudioPath { get; private set; }
        public string? AudioMime { get; private set; }
        public int? AudioDurationMs { get; private set; }
        public long? AudioSizeBytes { get; private set; }
        public string? AudioTranscription { get; private set; }

        // Costruttore privato per EF Core
        private Entry() { }

        /// <summary>
        /// Factory method per creare una nuova Entry
        /// </summary>
        public static Result<Entry> Create(
            Guid id,
            string userId,
            string text,
            DateTime createdAt,
            string? audioPath = null,
            string? audioMime = null,
            int? audioDurationMs = null,
            long? audioSizeBytes = null)
        {
            // Validazioni
            if (id == Guid.Empty)
                return Result.Fail<Entry>("L'ID non può essere vuoto");

            if (string.IsNullOrWhiteSpace(userId))
                return Result.Fail<Entry>("L'ID utente è obbligatorio");

            if (string.IsNullOrWhiteSpace(text))
                return Result.Fail<Entry>("Il testo è obbligatorio");

            if (text.Length > 100000)
                return Result.Fail<Entry>("Il testo non può superare i 100.000 caratteri");

            if (createdAt == default)
                return Result.Fail<Entry>("La data di creazione è obbligatoria");

            // Validazione audio coerente
            if (!string.IsNullOrWhiteSpace(audioPath))
            {
                if (string.IsNullOrWhiteSpace(audioMime))
                    return Result.Fail<Entry>("Se è presente l'audio, il MIME type è obbligatorio");
            }

            var entry = new Entry
            {
                Id = id,
                UserId = userId,
                Text = text,
                CreatedAt = createdAt,
                AudioPath = audioPath,
                AudioMime = audioMime,
                AudioDurationMs = audioDurationMs,
                AudioSizeBytes = audioSizeBytes
            };

            return Result.Ok(entry);
        }

        /// <summary>
        /// Aggiorna il testo dell'entry
        /// </summary>
        public Result UpdateText(string newText, string? audioTranscription = null)
        {
            if (string.IsNullOrWhiteSpace(newText))
                return Result.Fail("Il testo non può essere vuoto");

            if (newText.Length > 100000)
                return Result.Fail("Il testo non può superare i 100.000 caratteri");

            if (audioTranscription?.Length > 100000)
                return Result.Fail("La trascrizione audio non può superare i 100.000 caratteri");

            Text = newText;
            AudioTranscription = audioTranscription;
            UpdatedAt = DateTime.UtcNow;

            return Result.Ok();
        }

        /// <summary>
        /// Sostituisce l'audio esistente con un nuovo audio
        /// </summary>
        public Result<string?> ReplaceAudio(
            string? newAudioPath,
            string? newAudioMime,
            int? newAudioDurationMs,
            long? newAudioSizeBytes)
        {
            var oldAudioPath = AudioPath;

            // Se c'è un nuovo audio, validalo
            if (!string.IsNullOrWhiteSpace(newAudioPath))
            {
                if (string.IsNullOrWhiteSpace(newAudioMime))
                    return Result.Fail<string?>("Se è presente l'audio, il MIME type è obbligatorio");

                AudioPath = newAudioPath;
                AudioMime = newAudioMime;
                AudioDurationMs = newAudioDurationMs;
                AudioSizeBytes = newAudioSizeBytes;
            }
            else
            {
                // Rimuovi l'audio
                AudioPath = null;
                AudioMime = null;
                AudioDurationMs = null;
                AudioSizeBytes = null;
                AudioTranscription = null;
            }

            UpdatedAt = DateTime.UtcNow;

            // Ritorna il path del vecchio audio da eliminare (se presente)
            return Result.Ok(oldAudioPath);
        }

        /// <summary>
        /// Rimuove l'audio dall'entry
        /// </summary>
        public string? RemoveAudio()
        {
            var oldAudioPath = AudioPath;

            AudioPath = null;
            AudioMime = null;
            AudioDurationMs = null;
            AudioSizeBytes = null;
            AudioTranscription = null;
            UpdatedAt = DateTime.UtcNow;

            return oldAudioPath;
        }

        /// <summary>
        /// Verifica se l'entry appartiene all'utente specificato
        /// </summary>
        public bool BelongsToUser(string userId)
        {
            return UserId == userId;
        }

        /// <summary>
        /// Verifica se l'entry ha un audio associato
        /// </summary>
        public bool HasAudio()
        {
            return !string.IsNullOrWhiteSpace(AudioPath);
        }
    }
}

