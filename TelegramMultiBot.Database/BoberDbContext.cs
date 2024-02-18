using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;

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

            string connectionString = configuration["ConnectionString"];
            var serverVersion = GetServerVersion(connectionString);
            optionsBuilder.UseMySql(connectionString, serverVersion);
            return new BoberDbContext(optionsBuilder.Options);
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

    }
}

