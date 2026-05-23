using System.Text.Json;

namespace SuperStock.API.Helpers
{
    //// <summary>
    /// Convierte Dictionary<string,object> (proveniente de JSON) a Dictionary<string,string>
    /// que es el tipo que Cassandra acepta para map<text,text>.
    /// Valores no-string se convierten a su representacion JSON.
    /// </summary>
    public static class JsonValueNormalizer
    {
        public static Dictionary<string, string>? NormalizeToStringDictionary(Dictionary<string, object>? source)
        {
            if (source is null) return null;

            var result = new Dictionary<string, string>(source.Count);
            foreach (var (key, value) in source)
            {
                result[key] = ToStringValue(value);
            }
            return result;
        }

        private static string ToStringValue(object? value)
        {
            if (value is null) return string.Empty;
            if (value is string s) return s;
            if (value is JsonElement je)
            {
                return je.ValueKind switch
                {
                    JsonValueKind.String => je.GetString() ?? string.Empty,
                    JsonValueKind.Number => je.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => string.Empty,
                    _ => je.GetRawText()
                };
            }
            return value.ToString() ?? string.Empty;
        }
    }
}