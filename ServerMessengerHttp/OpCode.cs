using System.Text.Json;

namespace ServerMessengerHttp
{
    internal enum OpCode : byte
    {
        SendClientRSA = 0,
        ReceiveAes = 1,
        ReadyToReceive = 2,
        ReceiveRequestToCreateAcc = 3,
        ResponseRequestToCreateAcc = 4,
        VerificationProcess = 5,
        VerificationResult = 6,
    }

    internal static class OpCodeExtensions
    {
        public static OpCode GetOpCode(this JsonElement property)
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
    }
}
