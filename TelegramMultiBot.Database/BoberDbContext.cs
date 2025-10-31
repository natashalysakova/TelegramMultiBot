using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database;

public class BoberDbContext : DbContext
{
    public BoberDbContext(DbContextOptions options) : base(options)
    {
    }
    public virtual DbSet<ImageJob> Jobs { get; set; }
    public virtual DbSet<JobResult> JobResult { get; set; }
    public virtual DbSet<BotMessage> BotMessages { get; set; }

    public virtual DbSet<Host> Hosts { get; set; }
    public virtual DbSet<Model> Models { get; set; }
    public virtual DbSet<Settings> Settings { get; set; }

    public virtual DbSet<ReminderJob> Reminders { get; set; }
    public virtual DbSet<MonitorJob> Monitor { get; set; }
    public virtual DbSet<ElectricityLocation> ElectricityLocations { get; set; }
    public virtual DbSet<ElectricityHistory> ElectricityHistory { get; set; }
    public virtual DbSet<ElectricityGroup> ElectricityGroups { get; set; }

    public virtual DbSet<AssistantSubscriber> Assistants { get; set; }
    public virtual DbSet<ChatHistory> ChatHistory { get; set; }


    public void Seed()
    {
        var locations = new ElectricityLocation[]
        {
            new ElectricityLocation
            {
                Id = Guid.Parse("3309DEF8-F7AD-4474-B609-643086142802"),
                Url = "https://www.dtek-kem.com.ua/ua/shutdowns",
                Region = "kem",
            },
            new ElectricityLocation
            {
                Id = Guid.Parse("57E6C175-11D0-4B8A-83B1-1FC4925A7B58"),
                Url = "https://www.dtek-krem.com.ua/ua/shutdowns",
                Region = "krem",
            }
        };

        foreach (var location in locations)
        {
            var sameUrlDifferentId = this.ElectricityLocations.SingleOrDefault(x => x.Url == location.Url && x.Id != location.Id);
            if (sameUrlDifferentId != null)
            {
                this.ElectricityLocations.Remove(sameUrlDifferentId);
            }

            if (this.ElectricityLocations.Find(location.Id) == null)
            {
                this.ElectricityLocations.Add(location);
            }
        }
        SaveChanges();
    }
}

public class BoberDbContextFactory : IDesignTimeDbContextFactory<BoberDbContext>
{
    public BoberDbContext CreateDbContext(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings-design.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<BoberDbContext>();

        string? connectionString = configuration.GetConnectionString("db");
        if (connectionString == null)
        {
            throw new NullReferenceException(nameof(connectionString));
        }
        var serverVersion = GetServerVersion(connectionString);

        _ = optionsBuilder.UseMySql(connectionString, serverVersion);
        return new BoberDbContext(optionsBuilder.Options);
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