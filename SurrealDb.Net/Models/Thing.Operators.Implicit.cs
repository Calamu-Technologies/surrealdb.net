﻿namespace SurrealDb.Net.Models;

// 💡 Consider Source Generator to generate all possible implicit operator variants (table or id of type string, int, short, Guid, etc...)
// 💡 Consider client Source Generator to generate custom variants (from objects and arrays), the same way as the Thing.From source generation
// Note that it is not possible to define both types of a number range (eg. int and uint)

public partial class Thing
{
    public static implicit operator Thing((string table, string id) tuple)
    {
        return From(tuple.table, tuple.id);
    }

    public static implicit operator Thing((string table, int id) tuple)
    {
        return From(tuple.table, tuple.id);
    }

    public static implicit operator Thing((string table, long id) tuple)
    {
        return From(tuple.table, tuple.id);
    }

    public static implicit operator Thing((string table, short id) tuple)
    {
        return From(tuple.table, tuple.id);
    }

    public static implicit operator Thing((string table, byte id) tuple)
    {
        return From(tuple.table, tuple.id);
    }

    public static implicit operator Thing((string table, Guid id) tuple)
    {
        return From(tuple.table, tuple.id);
    }
}
