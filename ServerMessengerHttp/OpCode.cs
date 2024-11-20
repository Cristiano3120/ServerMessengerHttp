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
        UnexpectedError = 7,
    }
}
