﻿using System.Text.Json.Serialization;
using Dahomey.Cbor.Attributes;
using SurrealDb.Net.Internals.Json.Converters;

namespace SurrealDb.Net.Models;

/// <summary>
/// The abstract class for Record type.
/// </summary>
public abstract class Record : IRecord
{
    /// <summary>
    /// The id of the record
    /// </summary>
    [JsonConverter(typeof(ThingConverter))]
    [CborProperty("id")]
    [JsonPropertyName("id")]
    [CborIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] // 💡 ignore null value to prevent failure on Create operation
    public Thing? Id { get; set; }
}

/// <summary>
/// The interface for Record type.
/// </summary>
public interface IRecord
{
    /// <summary>
    /// The id of the record
    /// </summary>
    [JsonConverter(typeof(ThingConverter))]
    [JsonPropertyName("id")]
    [CborProperty("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] // 💡 ignore null value to prevent failure on Create operation
    [CborIgnoreIfDefault]
    public Thing? Id { get; set; }
}
