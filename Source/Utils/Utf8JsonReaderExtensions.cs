using System.Text.Json;

namespace MMM.Fabric;

public static class Utf8JsonReaderExtensions
{
    public static void SkipJunk(this ref Utf8JsonReader reader)
    {
        while (reader.TokenType is JsonTokenType.Comment or JsonTokenType.None)
        {
            reader.Skip();
        }
    }
}
