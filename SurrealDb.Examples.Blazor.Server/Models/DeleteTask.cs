using SurrealDb.Net.Models;

namespace SurrealDb.Examples.Blazor.Server.Models;

public class DeleteTask : Record<RecordId>
{
    internal const string Table = "delete_task";

    public RecordId Task { get; set; } = null!;
}
