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
using TelegramMultiBot.AiAssistant;
using TelegramMultiBot.Commands;
using TelegramMultiBot.Commands.Interfaces;
using TelegramMultiBot.Database;
using TelegramMultiBot.Database.Enums;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;
using TelegramMultiBot.Database.Profiles;
using TelegramMultiBot.Database.Services;
using TelegramMultiBot.ImageCompare;
using TelegramMultiBot.ImageGenerators;
using TelegramMultiBot.ImageGenerators.Automatic1111;
using ServiceKeyAttribute = TelegramMultiBot.Commands.ServiceKeyAttribute;

internal class Program
{
    public static void Main(string[] args)
    {
        //if (args.Length == 0)
        //{
        //    Console.WriteLine("provide argument for env (prod or dev)");
        //    return;
        //}

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Console.WriteLine("Welcome to Bober Bot " + environment);
        ServiceProvider serviceProvider = RegisterServices(args);

        var context = serviceProvider.GetRequiredService<BoberDbContext>();
        if (context.Database.GetPendingMigrations().Any())
        {
            context.Database.Migrate();
        }
        if (!context.Settings.Any())
        {
            SetDefaultSettings(context);
        }
        if (!context.Models.Any())
        {
            AddModels(context);
        }

        var bot = serviceProvider.GetRequiredService<BotService>();
        bot.Run();
    }

    private static void AddModels(BoberDbContext context)
    {
        context.Models.Add(new Model()
        {
            Name = "dreamshaper",
            CGF = 2,
            CLIPskip = 1,
            Path = "SDXL/dreamshaperXL_v21TurboDPMSDE.safetensors",
            Sampler = "dpmpp_sde",
            Scheduler = "karras",
            Steps = 6,
            Version = ModelVersion.SDXL
        });

        context.SaveChanges();
    }

    private static void SetDefaultSettings(BoberDbContext context)
    {
        context.AddSetting("Automatic1111", "OutputDirectory", "automatic");
        context.AddSetting("Automatic1111", "PayloadPath", "ImageGeneration/Automatic1111/Payload");
        context.AddSetting("Automatic1111", "UpscalePath", "ImageGeneration/Automatic1111/Upscales");

        context.AddSetting("ComfyUI", "OutputDirectory", "comfy");
        context.AddSetting("ComfyUI", "PayloadPath", "ImageGeneration/ComfyUI/Payload");
        context.AddSetting("ComfyUI", "InputDirectory", "/home/input");
        context.AddSetting("ComfyUI", "NoiseStrength", "0.3");
        context.AddSetting("ComfyUI", "VegnietteIntensity", "0.3");
        context.AddSetting("ImageGeneration", "ActiveJobs", "1");
        context.AddSetting("ImageGeneration", "BaseImageDirectory", "images");
        context.AddSetting("ImageGeneration", "BatchCount", "1");
        context.AddSetting("ImageGeneration", "DatabaseCleanupInterval", "3600");
        context.AddSetting("ImageGeneration", "DefaultModel", "dreamshaper");
        context.AddSetting("ImageGeneration", "DownloadDirectory", "download");
        context.AddSetting("ImageGeneration", "HiresFixDenoise", "0.35");
        context.AddSetting("ImageGeneration", "JobAge", "172800");
        context.AddSetting("ImageGeneration", "JobLimitPerUser", "3");
        context.AddSetting("ImageGeneration", "MaxGpuUtil", "20");
        context.AddSetting("ImageGeneration", "RemoveFiles", "True");
        context.AddSetting("ImageGeneration", "UpscaleModel", "4x-UltraSharp.pth");
        context.AddSetting("ImageGeneration", "UpscaleMultiplier", "4");
        context.AddSetting("ImageGeneration", "Watermark", "True");
        context.AddSetting("ImageGeneration", "ReciverPort", "5267");


        context.SaveChanges();
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
            _ = options.UseLazyLoadingProxies();
            _ = options.UseMySql(connectionString, serverVersion, op2 => { op2.EnableRetryOnFailure(100, TimeSpan.FromSeconds(30), null); });
            _ = options.LogTo(Console.WriteLine, LogLevel.Warning);
            _ = options.EnableDetailedErrors();
        }, ServiceLifetime.Transient);
        _ = serviceCollection.AddTransient<IImageDatabaseService, ImageService>();
        _ = serviceCollection.AddTransient<IBotMessageDatabaseService, BotMessageService>();
        _ = serviceCollection.AddTransient<ISqlConfiguationService, ConfigurationService>();
        _ = serviceCollection.AddTransient<IReminderDataService, ReminderService>();
        _ = serviceCollection.AddTransient<IMonitorDataService, MonitorDataService>();
        _ = serviceCollection.AddTransient<IAssistantDataService, AssistantDataService>();

        _ = serviceCollection.AddTransient<CleanupService>();

        _ = serviceCollection.AddTransient<SummarizeAiHelper>();

        var botKey = configuration["TG_TOKEN"];
        if (string.IsNullOrEmpty(botKey))
            throw new KeyNotFoundException("TG_TOKEN");

        _ = serviceCollection.AddSingleton(new TelegramBotClient(botKey, cancellationToken: new CancellationToken()) { Timeout = TimeSpan.FromSeconds(600) });

        _ = serviceCollection.AddScoped<BotService>();
        _ = serviceCollection.AddScoped<TelegramClientWrapper>();
        _ = serviceCollection.AddScoped<ImageGenerator>();

        _ = serviceCollection.AddSingleton<JobManager>();
        _ = serviceCollection.AddSingleton<DialogManager>();
        _ = serviceCollection.AddSingleton<ImageGenearatorQueue>();

        _ = serviceCollection.AddSingleton<MonitorService>();

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
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile($"appsettings.json", false, true)
            .AddJsonFile($"appsettings.{environment}.json", true, true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();
    }
}
