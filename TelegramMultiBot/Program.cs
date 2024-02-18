// See https://aka.ms/new-console-template for more information
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using TelegramMultiBot.Commands;
using Microsoft.AspNetCore.Builder;
using ServiceKeyAttribute = TelegramMultiBot.Commands.ServiceKeyAttribute;
using Bober.Library.Contract;
using TelegramMultiBot;

internal class Program
{

    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("provide argument for env (prod or dev)");
            return;
        }

        var builder = WebApplication.CreateBuilder();
        Console.WriteLine("Welcome to Bober " + args[0]);

        IConfiguration configuration = SetupConfiguration(args);
        builder.Services.AddSingleton(configuration);

        builder.Services.AddLogging(loggerBuilder =>
        {
            loggerBuilder.AddConfiguration(configuration.GetSection("Logging"));
            loggerBuilder.ClearProviders();
            loggerBuilder.AddConsole();
        });



        var botKey = configuration["token"];
        if (string.IsNullOrEmpty(botKey))
            throw new KeyNotFoundException("token");

        builder.Services.AddSingleton(new TelegramBotClient(botKey) { Timeout = TimeSpan.FromSeconds(600) });

        builder.Services.AddSingleton<BotService>();
        builder.Services.AddHostedService(provider => provider.GetService<BotService>());

        builder.Services.AddScoped<BoberApiClient>();
        builder.Services.AddSingleton<JobManager>();
        builder.Services.AddSingleton<DialogManager>();

        RegisterMyServices<IDialogHandler>(builder.Services);
        RegisterMyKeyedServices<ICommand>(builder.Services);

        builder.Services.AddScoped<DialogHandlerFactory>();

        var app = builder.Build();
        

        app.UseHttpsRedirection();
        app.Run();


        //var bot = app.Services.GetService<BotService>();
        //CancellationTokenSource cancellationToken = new CancellationTokenSource();
        //bot.Run(cancellationToken);



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



        var botKey = configuration["token"];
        if (string.IsNullOrEmpty(botKey))
            throw new KeyNotFoundException("token");

        serviceCollection.AddSingleton(new TelegramBotClient(botKey) { Timeout = TimeSpan.FromSeconds(600)});

        serviceCollection.AddScoped<BotService>();
        serviceCollection.AddScoped<JobManager>();
        serviceCollection.AddScoped<DialogManager>();

        RegisterMyServices<IDialogHandler>(serviceCollection);
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
