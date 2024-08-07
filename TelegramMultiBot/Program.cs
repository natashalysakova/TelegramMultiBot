// See https://aka.ms/new-console-template for more information
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using MySqlConnector;
using Telegram.Bot;
using TelegramMultiBot;
using TelegramMultiBot.Commands;
using TelegramMultiBot.Commands.Interfaces;
using TelegramMultiBot.Database;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;
using TelegramMultiBot.Database.Profiles;
using TelegramMultiBot.Database.Services;
using TelegramMultiBot.ImageGenerators;
using TelegramMultiBot.ImageGenerators.Automatic1111;
using ServiceKeyAttribute = TelegramMultiBot.Commands.ServiceKeyAttribute;

internal class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("provide argument for env (prod or dev)");
            return;
        }

        Console.WriteLine("Welcome to Bober " + args[0]);

        ServiceProvider serviceProvider = RegisterServices(args);

        var context = serviceProvider.GetRequiredService<BoberDbContext>();
        if (context.Database.GetPendingMigrations().Any())
        {
            context.Database.Migrate();
        }

        var bot = serviceProvider.GetRequiredService<BotService>();
        bot.Run();
    }

    private static ServiceProvider RegisterServices(string[] args)
    {
        IConfiguration configuration = SetupConfiguration(args);
        var serviceCollection = new ServiceCollection();
        _ = serviceCollection.AddSingleton(configuration);

        


        Action<ILoggingBuilder> builder = (builder) =>
        {
            _ = builder.AddConfiguration(configuration.GetSection("Logging"));
            _ = builder.ClearProviders();
            _ = builder.AddConsole();
        };
        _ = serviceCollection.AddLogging(builder);
        var logger = LoggerFactory.Create(builder).CreateLogger<Program>();


        string? connectionString = configuration.GetConnectionString("db");
        var serverVersion = GetServerVersion(connectionString, logger);

        _ = serviceCollection.AddDbContext<BoberDbContext>(options =>
        {
            _ = options.UseMySql(connectionString, serverVersion, op2 => { op2.EnableRetryOnFailure(100, TimeSpan.FromSeconds(30), null); });
            _ = options.LogTo(Console.WriteLine, LogLevel.Warning);
            _ = options.EnableDetailedErrors();
        }, ServiceLifetime.Transient);
        _ = serviceCollection.AddTransient<IImageDatabaseService, ImageService>();
        _ = serviceCollection.AddTransient<IBotMessageDatabaseService, BotMessageService>();
        _ = serviceCollection.AddTransient<ISqlConfiguationService, ConfigurationService>();
        _ = serviceCollection.AddTransient<IReminderDataService, ReminderService>();

        _ = serviceCollection.AddTransient<CleanupService>();

        var botKey = configuration["token"];
        if (string.IsNullOrEmpty(botKey))
            throw new KeyNotFoundException("token");

        _ = serviceCollection.AddSingleton(new TelegramBotClient(botKey, cancellationToken: new CancellationToken()) { Timeout = TimeSpan.FromSeconds(600) });

        _ = serviceCollection.AddScoped<BotService>();
        _ = serviceCollection.AddScoped<TelegramClientWrapper>();
        _ = serviceCollection.AddScoped<ImageGenerator>();

        _ = serviceCollection.AddSingleton<JobManager>();
        _ = serviceCollection.AddSingleton<DialogManager>();
        _ = serviceCollection.AddSingleton<ImageGenearatorQueue>();

        _ = RegisterMyServices<IDialogHandler>(serviceCollection);
        _ = RegisterMyServices<IDiffusor>(serviceCollection);
        RegisterMyKeyedServices<ICommand>(serviceCollection);

        _ = serviceCollection.AddScoped<DialogHandlerFactory>();

        var cfg = new MapperConfiguration(c =>
        {
            c.AddMaps(typeof(ImageJobProfile));
        });
        cfg.AssertConfigurationIsValid();
        _ = serviceCollection.AddTransient(x => { return cfg.CreateMapper(); });

        return serviceCollection.BuildServiceProvider();
    }

    private static ServerVersion GetServerVersion(string? connectionString, ILogger logger)
    {
        ServerVersion? version = default;
        logger.LogInformation("connecting to " + connectionString);
        do
        {
            try
            {
                version = ServerVersion.AutoDetect(connectionString);
                logger.LogInformation("Success");
            }
            catch (Exception ex)
            {
                logger.LogInformation(ex.Message);

                if (ex.Message.Contains("Unable to connect to any of the specified MySQL hosts"))
                {
                    logger.LogInformation("Trying in 15 seconds");
                    Thread.Sleep(15000);
                }
                else
                {
                    logger.LogInformation("Trying in 60 seconds");
                    Thread.Sleep(60000);
                }
            }
        }
        while (version is null);
        return version;
    }

    private static void RegisterMyKeyedServices<T>(IServiceCollection serviceCollection)
    {
        var services = RegisterMyServices<T>(serviceCollection);
        var types = GetTypes<T>();

        foreach (var item in types)
        {
            var key = item.GetAttributeValue((ServiceKeyAttribute c) => { return c.Command; });
            if (key != null)
            {
                _ = services.AddKeyedScoped(typeof(T), key, item);
            }
        }
    }

    private static IEnumerable<Type> GetTypes<T>()
    {
        var type = typeof(T);
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => type.IsAssignableFrom(p) && !p.IsAbstract);

        return types;
    }

    public static IServiceCollection RegisterMyServices<T>(IServiceCollection serviceCollection)
    {
        var services = GetTypes<T>();

        foreach (var item in services)
        {
            _ = serviceCollection.AddTransient(typeof(T), item);
        }

        return serviceCollection;
    }

    private static IConfiguration SetupConfiguration(string[] args)
    {
        //var environment = Environment.GetEnvironmentVariable("ENV_NAME");
        //if (string.IsNullOrEmpty(environment))
        //{
        //    Console.WriteLine("add 'export ENV_NAME=({prod or dev})' to fix this");
        //}

        var environment = args[0].Split('=').Last();

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
}
