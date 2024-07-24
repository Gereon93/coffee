using MongoDB.Driver;

namespace CoffeeApi
{
    public class Program
    {
        private static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            string? user = Environment.GetEnvironmentVariable("MONGO_USER");
            string? password = Environment.GetEnvironmentVariable("MONGO_PASSWORD");
            if (user == null || password == null)
            {
                throw new Exception("MONGO_USER and MONGO_PASSWORD environment variables must be set");
            }
            string mongoConnectionString = $"mongodb://{user}:{password}@192.168.2.143:27017/";

            // Register MongoClient with DI
            builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoConnectionString));

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Coffee API V1");
            });

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}