using System.Text.Json;

namespace ServerMessengerHttp
{
    internal static class JsonExtensions
    {
        internal static DateOnly GetDateOnly(this JsonElement property)
        {
            return property.ValueKind == JsonValueKind.String && DateOnly.TryParse(property.GetString(), out DateOnly birthDate)
                ? birthDate
                : throw new Exception("The data wasn't a valid DateOnly object.");
        }
    }
}
