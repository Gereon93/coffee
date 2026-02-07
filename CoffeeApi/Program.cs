using CoffeeApi.Infrastructure;
using CoffeeApi.Middleware;
using CoffeeApi.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

namespace CoffeeApi
{
    public static class Program
    {
        private static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddOpenApi();

            // ===== EQ900 Services (SQLite) =====
            var connectionString = builder.Configuration.GetConnectionString("Default")
                ?? "Data Source=coffee.db";

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(connectionString));

            builder.Services.AddScoped<ISnapshotService, SnapshotService>();

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            var app = builder.Build();

            // Ensure database is created
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
            }

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Scalar API Documentation (replaces Swagger)
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                options.Title = "Coffee Analytics Hub API";
                options.Theme = ScalarTheme.BluePlanet;
            });

            app.UseCors();

            // API Key Authentication for protected endpoints
            app.UseApiKeyAuthentication();

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}
