using Memoa.Application.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Memoa.Application
{
    /// <summary>
    /// Extension methods per registrare i servizi del layer Application
    /// </summary>
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            // MediatR per gestire Commands e Queries
            services.AddMediatR(options =>
            {
                options.RegisterServicesFromAssemblyContaining<GetUserRequestHandler>();
            });

            // Qui in futuro si possono aggiungere:
            // - FluentValidation validators
            // - AutoMapper profiles
            // - Pipeline behaviors per logging, validation, ecc.

            return services;
        }
    }
}
