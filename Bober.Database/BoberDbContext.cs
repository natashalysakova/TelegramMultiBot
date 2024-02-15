using Microsoft.EntityFrameworkCore;


namespace Bober.Database
{
    public class BoberDbContext : DbContext
    {
        public BoberDbContext(DbContextOptions options) : base(options)
        {
        }

        public virtual DbSet<ImageJob> Jobs { get; set; }
        public virtual DbSet<JobResult> JobResult { get; set; }

    }
}

