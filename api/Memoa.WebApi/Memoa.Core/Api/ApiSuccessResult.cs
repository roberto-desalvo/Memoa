using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Memoa.Core.Api
{
    public class ApiSuccessResult<T>
    {
        public T? Data { get; set; }
        public HttpStatusCode HttpStatusCode { get; set; } = HttpStatusCode.OK;
        public int ResultCode { get; set; } = 0;
        public string ResultDescription { get; set; } = "Success";
        public string? Details { get; set; } = "Operazione completata con successo";
        public ValidationProblemDetails? ProblemDetails { get; set; }
        public IDictionary<string, object?> Parameters { get; set; } = new Dictionary<string, object?>();
    }
}
