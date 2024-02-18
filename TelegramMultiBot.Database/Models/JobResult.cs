using System.ComponentModel.DataAnnotations.Schema;

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

