using SurrealDb.Net.Models;

namespace SurrealDb.Examples.MinimalApis.Models;

public class Todo : Record<RecordId>
{
    internal const string Table = "todo";

    public string? Title { get; set; }
    public DateOnly? DueBy { get; set; } = null;
    public bool IsComplete { get; set; } = false;
}
