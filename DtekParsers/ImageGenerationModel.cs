namespace DtekParsers;

public class ImageGenerationModel
{
    public byte[]? ImageData { get; set; }
    public string? Group { get; init; }

    public long? Date { get; init; }
    public bool IsPlanned { get; init; }
    public int RowNumber { get; init; }
    public required string HtmlContent { get; init; }
}