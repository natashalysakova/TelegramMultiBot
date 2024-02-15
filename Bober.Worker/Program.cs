using Bober.Database;
using Bober.Database.Services;
using Bober.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        IConfiguration configuration = SetupConfiguration(args);
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(configuration);



        builder.Services.AddHostedService<ImageRenderQuery>();

        string connectionString = configuration["ConnectionString"];
        var serverVersion = GetServerVersion(connectionString);

        builder.Services.AddDbContext<BoberDbContext>(options =>
        {
            options.UseMySql(connectionString, serverVersion);
            options.LogTo(Console.WriteLine, LogLevel.Warning);
        });
        builder.Services.AddScoped<ImageDatabaseService>();
        builder.Services.AddScoped<CleanupService>();


        var host = builder.Build();
        host.Run();
    }
    private static IConfiguration SetupConfiguration(string[] args)
    {
        //var environment = Environment.GetEnvironmentVariable("ENV_NAME");
        //if (string.IsNullOrEmpty(environment))
        //{
        //    Console.WriteLine("add 'export ENV_NAME=({prod or dev})' to fix this");
        //}

        var environment = args[0];

        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile($"tokens.json", false, true)
            .AddJsonFile($"tokens.{environment}.json", false, true)
            .AddJsonFile($"appsettings.json", false, true)
            .AddJsonFile($"appsettings.{environment}.json", false, true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();
    }
    private static ServerVersion GetServerVersion(string? connectionString)
    {
        ServerVersion version = default;

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
                    throw ex;
                }
            }
        }
        while (version is null);
        return version;
    }

}