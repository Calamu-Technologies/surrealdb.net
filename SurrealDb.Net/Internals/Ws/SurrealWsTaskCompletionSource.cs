namespace SurrealDb.Net.Internals.Ws;

internal class SurrealWsTaskCompletionSource : TaskCompletionSource<SurrealDbWsOkResponse>
{
    public SurrealDbWsRequestPriority Priority { get; }

    public SurrealWsTaskCompletionSource(
        TaskCreationOptions options,
        SurrealDbWsRequestPriority priority
    )
        : base(options)
    {
        Priority = priority;
    }
}
