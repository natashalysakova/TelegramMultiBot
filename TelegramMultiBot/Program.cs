// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot;
using TelegramMultiBot.Commands;
using TelegramMultiBot.ImageGenerators;
using TelegramMultiBot.ImageGenerators.Automatic1111;
using TelegramMultiBot.Properties;
using ServiceKeyAttribute = TelegramMultiBot.Commands.ServiceKeyAttribute;

internal class Program
{
    public static void Main(string[] args)
    {
        if(args.Length == 0)
        {
            Console.WriteLine("provide argument for env (prod or dev)");
            return;
        }

        Console.WriteLine("Welcome to Bober " + args[0]);

        ServiceProvider serviceProvider = RegisterServices(args);

        var bot = serviceProvider.GetService<BotService>();

        CancellationTokenSource cancellationToken = new CancellationTokenSource();
        bot.Run(cancellationToken);
    }

    private static ServiceProvider RegisterServices(string[] args)
    {
        IConfiguration configuration = SetupConfiguration(args);
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddLogging(loggerBuilder =>
        {
            loggerBuilder.AddConfiguration(configuration.GetSection("Logging"));
            loggerBuilder.ClearProviders();
            loggerBuilder.AddConsole();
        });

        serviceCollection.AddSingleton(configuration);

        var botKey = configuration["token"];
        if (string.IsNullOrEmpty(botKey))
            throw new KeyNotFoundException("token");

        serviceCollection.AddSingleton(new TelegramBotClient(botKey));

        serviceCollection.AddSingleton<BotService>();
        serviceCollection.AddSingleton<JobManager>();
        serviceCollection.AddSingleton<DialogManager>();
        serviceCollection.AddSingleton<ImageGenearatorQueue>();
        serviceCollection.AddScoped<ImageGenerator>();

        RegisterMyServices<IDialogHandler>(serviceCollection);
        RegisterMyServices<IDiffusor>(serviceCollection);
        RegisterMyKeyedServices<ICommand>(serviceCollection);

        serviceCollection.AddScoped<DialogHandlerFactory>();

        return serviceCollection.BuildServiceProvider();
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
