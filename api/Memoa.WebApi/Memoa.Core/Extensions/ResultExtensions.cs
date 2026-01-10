using FluentResults;
using Memoa.Core.Api;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Memoa.Core.Extensions
{
    public static class ResultExtensions
    {
        public static string GetErrorMessages(this IResultBase result)
        {
            var errors = result.Errors
                .Where(e => e.GetType() != typeof(ExceptionalError))
                .Select(e => e.Message) ?? [];
            return string.Join("; ", errors);
        }

        public static IActionResult GetSuccessMessages<T>(this Result<T> result)
        {
            return new OkObjectResult(new ApiSuccessResult<T> { Data = result.ValueOrDefault });
        }
    }
}
