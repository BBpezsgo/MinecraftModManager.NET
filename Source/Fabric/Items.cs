using System.Collections;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MMM.Fabric;

public class Items<T>
{

}

public class SingleItem<T>(T item) : Items<T>
{
    public T Item => item;

    public static implicit operator T(SingleItem<T> v) => v.Item;

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
    public IEnumerator<T> GetEnumerator() => items.GetEnumerator();
    public int IndexOf(T item) => items.IndexOf(item);
    public void Insert(int index, T item) => items.Insert(index, item);
    public bool Remove(T item) => items.Remove(item);
    public void RemoveAt(int index) => items.RemoveAt(index);
    IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();

    public override string? ToString() => items.ToString();
    public override bool Equals(object? obj) => items.Equals(obj);
    public override int GetHashCode() => items.GetHashCode();
}

public class ItemsJsonConverter<T> : JsonConverter<Items<T>>
{
    public override Items<T> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        reader.SkipJunk();
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            List<T> items = [];
            while (reader.TokenType != JsonTokenType.EndArray)
            {
                reader.SkipJunk();
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    reader.Read();
                    break;
                }
                items.Add(JsonSerializer.Deserialize<T>(ref reader, options) ?? throw new JsonException());
            }
            return new MultipleItems<T>(items);
        }
        else
        {
            return new SingleItem<T>(JsonSerializer.Deserialize<T>(ref reader, options) ?? throw new JsonException());
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
                writer.WriteStringValue(single.ToString());
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
