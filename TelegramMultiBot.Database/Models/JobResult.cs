using System.ComponentModel.DataAnnotations.Schema;
namespace TelegramMultiBot.Database.Models;

public class JobResult
{ 

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public virtual ImageJob? Job { get; set; }
    public required string FilePath { get; set; }
    public string? Info { get; set; }
    public int Index { get; set; }
    public double RenderTime { get; set; }

}

