using Bober.Database.Services;
using Bober.Database;
using Bober.Worker.ImageGeneration;
using Bober.Worker;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Bober.Library.Interfaces;
using AutoMapper;
using Bober.Automapper;
using Microsoft.Extensions.DependencyInjection;

internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("provide argument for env (prod or dev)");
            return;
        }

        var builder = WebApplication.CreateBuilder(args);
        // Add services to the container.

        IConfiguration configuration = SetupConfiguration(args);

        builder.Services.AddSingleton(configuration);

        builder.Services.AddLogging(loggerBuilder =>
        {
            loggerBuilder.AddConfiguration(configuration.GetSection("Logging"));
            loggerBuilder.ClearProviders();
            loggerBuilder.AddConsole();
            loggerBuilder.AddFilter("Microsoft.EntityFrameworkCore*", LogLevel.Warning);
        });


        string? connectionString = configuration["ConnectionString"];

        if (connectionString is null)
        {
            throw new ArgumentNullException(nameof(connectionString));
        }

        var serverVersion = GetServerVersion(connectionString);

        builder.Services.AddDbContext<BoberDbContext>(options =>
        {
            options.UseMySql(connectionString, serverVersion);
            options.LogTo(Console.WriteLine, LogLevel.Warning);
        });

        builder.Services.AddScoped<IDatabaseService, ImageDatabaseService>();
        builder.Services.AddScoped<ImageGenerator>();
        builder.Services.AddScoped<CleanupService>();
        builder.Services.AddHostedService<ImageRenderQuery>();


        var cfg = new MapperConfiguration(c =>
        {
            c.AddMaps(typeof(ImageJobProfile));
        });
        cfg.AssertConfigurationIsValid();
        builder.Services.AddTransient<IMapper>(x => { return cfg.CreateMapper(); });




        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        var app = builder.Build();

        using(var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetService<BoberDbContext>();
            context.Database.Migrate();
            context.SaveChanges();
        }


        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();
        app.Run();
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
    private static ServerVersion? GetServerVersion(string connectionString)
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