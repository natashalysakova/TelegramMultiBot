// See https://aka.ms/new-console-template for more information
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using System;
using System.Reflection;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot;
using TelegramMultiBot.Commands;
using TelegramMultiBot.Database;
using TelegramMultiBot.ImageGeneration;
using TelegramMultiBot.ImageGenerators;
using TelegramMultiBot.ImageGenerators.Automatic1111;
using TelegramMultiBot.Properties;
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
        context.Database.Migrate();

        var bot = serviceProvider.GetService<BotService>();

        CancellationTokenSource cancellationToken = new CancellationTokenSource();
        bot.Run(cancellationToken);
    }

    private static ServiceProvider RegisterServices(string[] args)
    {
        IConfiguration configuration = SetupConfiguration(args);
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(configuration);

        serviceCollection.AddLogging(loggerBuilder =>
        {
            loggerBuilder.AddConfiguration(configuration.GetSection("Logging"));
            loggerBuilder.ClearProviders();
            loggerBuilder.AddConsole();
        });

        string connectionString = configuration["ConnectionString"];
        var serverVersion = GetServerVersion(connectionString);

        serviceCollection.AddDbContext<BoberDbContext>(options =>
        {
            options.UseMySql(connectionString, serverVersion);
            options.LogTo(Console.WriteLine, LogLevel.Warning);
        });
        serviceCollection.AddSingleton<ImageDatabaseService>();
        serviceCollection.AddScoped<CleanupService>();


        var botKey = configuration["token"];
        if (string.IsNullOrEmpty(botKey))
            throw new KeyNotFoundException("token");

        serviceCollection.AddSingleton(new TelegramBotClient(botKey) { Timeout = TimeSpan.FromSeconds(600)});

        serviceCollection.AddScoped<BotService>();
        serviceCollection.AddScoped<ImageGenerator>();

        serviceCollection.AddSingleton<JobManager>();
        serviceCollection.AddSingleton<DialogManager>();
        serviceCollection.AddSingleton<ImageGenearatorQueue>();

        RegisterMyServices<IDialogHandler>(serviceCollection);
        RegisterMyServices<IDiffusor>(serviceCollection);
        RegisterMyKeyedServices<ICommand>(serviceCollection);

        serviceCollection.AddScoped<DialogHandlerFactory>();

        return serviceCollection.BuildServiceProvider();
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

    private static void RegisterMyKeyedServices<T>(IServiceCollection serviceCollection)
    {
        var services = RegisterMyServices<T>(serviceCollection);
        var types = GetTypes<T>();

        foreach (var item in types)
        {
            var key = item.GetAttributeValue((ServiceKeyAttribute c) => { return c.Command; });
            if (key != null)
            {
                services.AddKeyedScoped(typeof(T), key, item);
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
            serviceCollection.AddScoped(typeof(T), item);
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
}
