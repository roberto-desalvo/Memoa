using System.Threading;
using System.Threading.Tasks;

namespace Memoa.Application.Services
{
    public class AudioNotFoundException : Exception
    {
        public AudioNotFoundException(string message) : base(message) { }
    }

    public interface IAudioStorageService
    {
        /// <summary>
        /// Deletes an audio object by its path (relative to the bucket).
        /// </summary>
        /// <param name="audioPath">Relative object path, e.g. "userId/entryId.webm"</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <exception cref="AudioNotFoundException">Thrown when audio file is not found (404)</exception>
        /// <exception cref="Exception">Thrown for other storage errors</exception>
        Task DeleteAudioAsync(string audioPath, CancellationToken cancellationToken = default);
    }
}
