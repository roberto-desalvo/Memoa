using FluentResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Memoa.Core.Errors
{
    public class UnprocessableEntityError(string message) : Error(message)
    {
        public static Result<T> ResultFail<T>(string? message = null) => Result.Fail<T>(new InvalidInputError(message ?? "Errore validazione dati di input"));
        public static Result ResultFail(string? message = null) => Result.Fail(new InvalidInputError(message ?? "Errore validazione dati di input"));
    }
}
