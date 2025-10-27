using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using TelegramMultiBot.Database;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorPages();

        var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile($"appsettings.json", false, true)
                    .AddEnvironmentVariables()
        .AddCommandLine(args)
        .Build();



        builder.Services.AddSingleton<IConfiguration>(configuration);
        
        string? connectionString = configuration.GetConnectionString("db");
        var serverVersion = GetServerVersion(connectionString);

        builder.Services.AddDbContext<BoberDbContext>(options =>
        {
            _ = options.UseMySql(connectionString, serverVersion);
            _ = options.LogTo(Console.WriteLine, LogLevel.Warning);
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.MapRazorPages();

        app.Run();
    }

    private static ServerVersion GetServerVersion(string? connectionString)
    {
        ServerVersion? version = default;

        do
        {
            try
            {
                Console.WriteLine("connecting to " + connectionString);
                version = ServerVersion.AutoDetect(connectionString);
                Console.WriteLine("Success");
            }
            catch (MySqlException ex)
            {
                if (ex.Message.Contains("Unable to connect to any of the specified MySQL hosts"))
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine("Trying in 5 seconds");
                    Thread.Sleep(5000);
                }
                else
                {
                    throw;
                }
            }
        }
        while (version is null);
        return version;
    }
}