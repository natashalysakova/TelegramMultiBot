using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
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

    }

    public class BoberDbContextFactory : IDesignTimeDbContextFactory<BoberDbContext>
    {

        public BoberDbContext CreateDbContext(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("devsettings.json")
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<BoberDbContext>();

            string connectionString = configuration.GetConnectionString("db");
            var serverVersion = GetServerVersion(connectionString);
            optionsBuilder.UseMySql(connectionString, serverVersion);
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

