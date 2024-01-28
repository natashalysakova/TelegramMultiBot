// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.Commands;

internal class Program
{
    public static void Main(string[] args)
    {
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
            loggerBuilder.ClearProviders();
            loggerBuilder.AddConsole();
#if DEBUG
            loggerBuilder.SetMinimumLevel(LogLevel.Debug);
#endif
        });

        serviceCollection.AddSingleton(configuration);
#if DEBUG
        var botKey = configuration["token_debug"];
#else
        var botKey = configuration["token"];
#endif  
        if (string.IsNullOrEmpty(botKey))
            throw new KeyNotFoundException("token");

        serviceCollection.AddSingleton(new TelegramBotClient(botKey));

        serviceCollection.AddSingleton<BotService>();
        serviceCollection.AddSingleton<JobManager>();
        serviceCollection.AddSingleton<DialogManager>();

        RegisterMyServices<IDialogHandler>(serviceCollection);
        RegisterMyServices<ICommand>(serviceCollection);

        serviceCollection.AddScoped<DialogHandlerFactory>();

        return serviceCollection.BuildServiceProvider();
    }


    public static IServiceCollection RegisterMyServices<T>(IServiceCollection services)
    {
        var type = typeof(T);
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => type.IsAssignableFrom(p) && !p.IsAbstract);

        foreach (var item in types)
        {
            services.AddScoped(typeof(T), item);
        }

        return services;
    }

    private static IConfiguration SetupConfiguration(string[] args)
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("tokens.json")
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();
    }
}
