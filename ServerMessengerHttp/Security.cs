using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace ServerMessengerHttp
{
    internal static class Security
    {
        private static readonly RSAParameters _privateKey;
        private static readonly RSAParameters _publicKey;

        static Security()
        {
            using var rsa = new RSACryptoServiceProvider(2048);
            {
                try
                {
                    _publicKey = rsa.ExportParameters(false);
                    _privateKey = rsa.ExportParameters(true);
                }
                finally
                {
                    rsa.PersistKeyInCsp = false;
                }
            }
        }

        internal static async Task SendRSAToClient(WebSocket client)
        {
            var payload = new
            {
                code = OpCode.SendClientRSA,
                publicKey = _publicKey,
            };
            var jsonString = JsonSerializer.Serialize(payload);
            await Server.SendPayloadAsync(client, jsonString, EncryptionMode.None);
        }
    }
}
