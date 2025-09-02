using System.Collections;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MMM;

public abstract class Items<T> : IEnumerable<T>
{
    public abstract IEnumerator<T> GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class SingleItem<T>(T item) : Items<T>
{
    public T Item => item;

    public static implicit operator T(SingleItem<T> v) => v.Item;

    public override IEnumerator<T> GetEnumerator()
    {
        yield return item;
    }

    public override string? ToString() => (item ?? throw new NullReferenceException()).ToString();
    public override bool Equals(object? obj)
    {
        if (obj is null && item is null) return true;
        if (obj is null || item is null) return false;
        return item.Equals(obj);
    }
    public override int GetHashCode() => (item ?? throw new NullReferenceException()).GetHashCode();
}

public class MultipleItems<T>(List<T> items) : Items<T>, IList<T>
{
    public T this[int index]
    {
        get => items[index];
        set => items[index] = value;
    }
    public int Count => items.Count;

    bool ICollection<T>.IsReadOnly => false;

    public void Add(T item) => items.Add(item);
    public void Clear() => items.Clear();
    public bool Contains(T item) => items.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);
    public override IEnumerator<T> GetEnumerator() => items.GetEnumerator();
    public int IndexOf(T item) => items.IndexOf(item);
    public void Insert(int index, T item) => items.Insert(index, item);
    public bool Remove(T item) => items.Remove(item);
    public void RemoveAt(int index) => items.RemoveAt(index);
    IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();

    public override string? ToString() => items.ToString();
    public override bool Equals(object? obj) => items.Equals(obj);
    public override int GetHashCode() => items.GetHashCode();
}

[JsonConverter(typeof(ItemsJsonConverter<>))]
public class ItemsJsonConverter<T> : JsonConverter<Items<T>>
{
    public override Items<T> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        JsonDocument doc = JsonDocument.ParseValue(ref reader);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            return new MultipleItems<T>([.. doc.RootElement.EnumerateArray().Select(v => v.Deserialize<T>(options) ?? throw new JsonException())]);
        }
        else
        {
            return new SingleItem<T>(doc.Deserialize<T>(options) ?? throw new JsonException());
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        Items<T> value,
        JsonSerializerOptions options)
    {
        switch (value)
        {
            case SingleItem<T> single:
                JsonSerializer.Serialize(writer, single.Item, options);
                break;
            case MultipleItems<T> multiple:
                writer.WriteStartArray();
                foreach (T item in multiple)
                {
                    JsonSerializer.Serialize(writer, item, options);
                }
                writer.WriteEndArray();
                break;
            default:
                throw new UnreachableException();
        }
    }
}
