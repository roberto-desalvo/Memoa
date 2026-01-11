using FluentResults;

namespace Memoa.Core.Errors
{
    public class AudioStorageError(string message) : Error(message)
    {
        public static Result<T> ResultFail<T>(string? message = null) => Result.Fail<T>(new AudioStorageError(message ?? "Errore durante l'eliminazione dell'audio"));
        public static Result ResultFail(string? message = null) => Result.Fail(new AudioStorageError(message ?? "Errore durante l'eliminazione dell'audio"));
    }
}
