using Memoa.Infrastructure.Data;
using Memoa.Infrastructure.Repositories;
using Memoa.Infrastructure.Services;
using Memoa.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Memoa.Application.Services;

namespace Memoa.Infrastructure
{
    /// <summary>
    /// Extension methods per registrare i servizi del layer Infrastructure
    /// </summary>
    public static class InfrastructureServiceExtensions
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Database Context
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            services.AddDbContext<MemoaDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Unit of Work
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Repositories
            services.AddScoped<IEntryRepository, EntryRepository>();

            // Current User Service
            services.AddScoped<ICurrentUserService, CurrentUserService>();

            // HttpClient per Supabase Storage
            services.AddHttpClient();

            // Supabase Storage Service
            services.Configure<SupabaseAudioStorageOptions>(
                configuration.GetSection("SupabaseStorage"));
            services.AddSingleton<IAudioStorageService, SupabaseAudioStorageService>();

            return services;
        }
    }
}
