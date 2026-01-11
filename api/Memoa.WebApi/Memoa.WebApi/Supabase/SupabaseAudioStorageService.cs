using Memoa.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace Memoa.WebApi.Supabase
{
    public class SupabaseAudioStorageService : IAudioStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly SupabaseAudioStorageOptions _options;
        private readonly ILogger<SupabaseAudioStorageService> _logger;

        public SupabaseAudioStorageService(
            IHttpClientFactory httpClientFactory,
            IOptions<SupabaseAudioStorageOptions> options,
            ILogger<SupabaseAudioStorageService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrWhiteSpace(_options.ServiceRoleKey))
            {
                _logger.LogWarning("ServiceRoleKey mancante per SupabaseAudioStorageService. Le operazioni di cancellazione audio verranno saltate.");
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);
            }
        }

        public async Task DeleteAudioAsync(string audioPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(audioPath))
            {
                throw new ArgumentException("audioPath non può essere null o vuoto", nameof(audioPath));
            }

            if (string.IsNullOrWhiteSpace(_options.ServiceRoleKey))
            {
                _logger.LogError("ServiceRoleKey non configurata. Impossibile eliminare audio: {AudioPath}", audioPath);
                throw new InvalidOperationException("ServiceRoleKey non configurata per Supabase Storage. Impossibile eliminare l'audio.");
            }

            if (string.IsNullOrWhiteSpace(_options.SupabaseUrl) || string.IsNullOrWhiteSpace(_options.Bucket))
            {
                _logger.LogError("SupabaseUrl o Bucket non configurati. Impossibile eliminare audio: {AudioPath}", audioPath);
                throw new InvalidOperationException("Configurazione Supabase incompleta: SupabaseUrl o Bucket mancante.");
            }

            // Supabase Storage REST API endpoint
            var url = $"{_options.SupabaseUrl}/storage/v1/object/{_options.Bucket}/{audioPath}";

            try
            {
                var response = await _httpClient.DeleteAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Audio eliminato da storage: bucket={Bucket}, path={Path}", _options.Bucket, audioPath);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Audio non trovato in storage (già eliminato): bucket={Bucket}, path={Path}", _options.Bucket, audioPath);
                    throw new AudioNotFoundException($"Audio non trovato: {audioPath}");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Errore eliminazione audio: StatusCode={StatusCode}, Error={Error}, Path={Path}",
                        response.StatusCode, error, audioPath);
                    throw new HttpRequestException($"Eliminazione audio fallita con status {response.StatusCode}: {error}");
                }
            }
            catch (AudioNotFoundException)
            {
                // Rilancia per permettere al chiamante di gestire il 404
                throw;
            }
            catch (HttpRequestException)
            {
                // Rilancia gli errori HTTP già formattati
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore generico nella cancellazione dell'audio: bucket={Bucket}, path={Path}",
                    _options.Bucket, audioPath);
                throw;
            }
        }
    }
}
