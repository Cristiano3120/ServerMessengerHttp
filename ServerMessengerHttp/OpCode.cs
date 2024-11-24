using System.Text.Json;

namespace ServerMessengerHttp
{
    internal enum OpCode : byte
    {
        UnexpectedError = 0,
        SendClientRSA = 1,
        ReceiveAes = 2,
        ReadyToReceive = 3,
        ReceiveRequestToCreateAcc = 4,
        ResponseRequestToCreateAcc = 5,
        VerificationProcess = 6,
        VerificationResult = 7,
        RequestToLogin = 8,
        ResponseToLogin = 9,
    }
}
