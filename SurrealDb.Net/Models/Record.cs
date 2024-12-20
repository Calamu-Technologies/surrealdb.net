using System.Text.Json.Serialization;
using Dahomey.Cbor.Attributes;
using SurrealDb.Net.Json;

namespace SurrealDb.Net.Models;

/// <summary>
/// The abstract class for Record type.
/// </summary>
public abstract class Record<T> : IRecord<T>
    where T : RecordId
{
    /// <summary>
    /// The id of the record
    /// </summary>
    [CborProperty("id")]
    [CborIgnoreIfDefault]
    public T? Id { get; set; }
}

/// <summary>
/// The interface for Record type.
/// </summary>
public interface IRecord<T>
    where T : RecordId
{
    /// <summary>
    /// The id of the record
    /// </summary>
    [CborProperty("id")]
    [CborIgnoreIfDefault]
    public T? Id { get; set; }
}
