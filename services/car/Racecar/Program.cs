
namespace Racecar
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddOpenApi();
            builder.Services.AddHealthChecks();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.MapHealthChecks("/health/startup");
            app.MapHealthChecks("/health/live");

            app.MapControllers();

            app.Run();
        }
    }
}
