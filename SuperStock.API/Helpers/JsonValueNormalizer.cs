using System.Text.Json;

namespace SuperStock.API.Helpers
{
    /// <summary>
    /// Convierte valores deserializados por System.Text.Json (especialmente JsonElement)
    /// a tipos CLR simples que MongoDB.Driver sí puede serializar dentro de
    /// Dictionary<string, object>.
    /// </summary>
    public static class JsonValueNormalizer
    {
        public static Dictionary<string, object>? NormalizeDictionary(Dictionary<string, object>? source)
        {
            if (source is null)
                return null;

            var normalized = new Dictionary<string, object>(source.Count);

            foreach (var (key, value) in source)
            {
                normalized[key] = NormalizeValue(value)!;
            }

            return normalized;
        }

        private static object? NormalizeValue(object? value)
        {
            if (value is null)
                return null;

            if (value is JsonElement jsonElement)
                return NormalizeJsonElement(jsonElement);

            if (value is Dictionary<string, object> dictionary)
                return NormalizeDictionary(dictionary);

            if (value is IEnumerable<object> list && value is not string)
                return list.Select(NormalizeValue).ToList();

            return value;
        }

        private static object? NormalizeJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject()
                    .ToDictionary(
                        property => property.Name,
                        property => NormalizeJsonElement(property.Value)!),

                JsonValueKind.Array => element.EnumerateArray()
                    .Select(NormalizeJsonElement)
                    .ToList(),

                JsonValueKind.String => element.GetString(),

                JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
                JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
                JsonValueKind.Number => element.GetDouble(),

                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,

                _ => element.GetRawText()
            };
        }
    }
}