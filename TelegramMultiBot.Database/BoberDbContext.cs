using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.Diagnostics;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database
{
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

        public virtual DbSet<AssistantSubscriber> Assistants { get; set; }
        public virtual DbSet<ChatHistory> ChatHistory { get; set; }
    }

    public class BoberDbContextFactory : IDesignTimeDbContextFactory<BoberDbContext>
    {
        public BoberDbContext CreateDbContext(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
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
}