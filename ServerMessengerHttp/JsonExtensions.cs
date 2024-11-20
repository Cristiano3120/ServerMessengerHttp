using System.Text.Json;

namespace ServerMessengerHttp
{
    internal static class JsonExtensions
    {
        internal static OpCode GetOpCode(this JsonElement property)
        {
            if (property.ValueKind != JsonValueKind.Number)
            {
                throw new InvalidOperationException("Cannot convert a non-numeric JSON property to an enum.");
            }

            byte value;
            try
            {
                value = property.GetByte();
            }
            catch (FormatException)
            {
                throw new Exception("The JSON number is not a valid integer.");
            }
            catch (OverflowException)
            {
                throw new Exception("The JSON number is outside the valid range for an OpCodeEnum value.");
            }

            return Enum.IsDefined(typeof(OpCode), value)
                ? (OpCode)value
                : throw new Exception("The JSON number does not correspond to any defined OpCodeEnum value.");
        }

        internal static DateOnly GetDateOnly(this JsonElement property)
        {
            return property.ValueKind == JsonValueKind.String && DateOnly.TryParse(property.GetString(), out DateOnly birthDate)
                ? birthDate
                : throw new Exception("The data wasn't a valid DateOnly object.");
        }
    }
}
