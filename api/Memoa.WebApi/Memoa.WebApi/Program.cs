using Memoa.Application.Handlers;
using Memoa.Domain.Data;
using Memoa.Domain.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpContextAccessor();

// EF Core + Postgres
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<MemoaDbContext>(options =>
    options.UseNpgsql(connectionString));

// Repositories
builder.Services.AddScoped<IEntryRepository, EntryRepository>();

builder.Services.AddMediatR(options =>
{
    _ = options.RegisterServicesFromAssemblyContaining<GetUserRequestHandler>();
});

var supabaseUrl = builder.Configuration["Supabase:Url"];
var supabaseAudience = builder.Configuration["Supabase:Audience"];
var authority = $"{supabaseUrl}/auth/v1";

builder.Services
  .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(options =>
  {
      options.Authority = authority;
      options.MetadataAddress = $"{authority}/.well-known/openid-configuration"; // opzionale ma esplicito

      options.TokenValidationParameters = new TokenValidationParameters
      {
          ValidateIssuer = true,
          ValidIssuer = authority,
          ValidateAudience = true,
          ValidAudience = supabaseAudience,
          ValidateLifetime = true,
      };
  });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Applica le migrations all'avvio in Development
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<MemoaDbContext>();
        await db.Database.MigrateAsync();
    }
    
    app.MapOpenApi();
}

app.UseCors("AllowLocalhost");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
