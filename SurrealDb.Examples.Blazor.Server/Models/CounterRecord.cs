using SurrealDb.Net.Models;

namespace SurrealDb.Examples.Blazor.Server.Models;

public class CounterRecord : Record<RecordId>
{
    public int Value { get; set; }
}
