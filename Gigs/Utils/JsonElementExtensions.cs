using System.Text.Json;

namespace Gigs.Utils;

public static class JsonElementExtensions
{
    public static JsonElement? GetPropertyOrDefault(this JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value))
        {
            return value;
        }
        return null;
    }
}
