using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

public class ImageJob
{
    public ImageJob()
    {
        Created = DateTime.Now;
        BotMessageId = -1;
    }

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public DateTime Created { get; set; }
    public DateTime Started { get; set; }
    public DateTime Finised { get; set; }
    public ImageJobStatus Status { get; set; }

    public long UserId { get; set; }
    public virtual ICollection<JobResult> Results { get; set; } = new List<JobResult>();
    public long ChatId { get; set; }
    public int MessageId { get; set; }
    public int? MessageThreadId { get; set; }
    public string? Text { get; set; }
    public int BotMessageId { get; set; }
    public bool PostInfo { get; set; }
    public  JobType Type { get; set; }

    public Guid? PreviousJobResultId { get; set; }
    public double UpscaleModifyer { get; set; }
}

public enum ImageJobStatus
{
    Queued, Running , Succseeded , Failed
}
public enum JobType
{
    Text2Image,
    HiresFix,
    Upscale,
    Info,
    Original,
    Actions,
}
public class JobResult
{ 

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public virtual ImageJob? Job { get; set; }
    public string FilePath { get; set; }
    public string? Info { get; set; }
    public int Index { get; set; }
    public TimeSpan RenderTime { get; set; }

}


public class Reminder
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; }
    public string Name { get; }
    public string Message { get; }
    public string Config { get; }
    public long ChatId { get; }

}

