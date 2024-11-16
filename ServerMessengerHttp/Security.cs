using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ServerMessengerHttp
{
    internal static class Security
    {
        private static readonly RSAParameters _privateKey;
        private static readonly RSAParameters _publicKey;
        internal static readonly ConcurrentDictionary<WebSocket, Aes> _clientsAes;

        static Security()
        {
            _clientsAes = new ConcurrentDictionary<WebSocket, Aes>();
            using var rsa = new RSACryptoServiceProvider(4096);
            {
                try
                {
                    rsa.PersistKeyInCsp = false;
                    _publicKey = rsa.ExportParameters(false);
                    _privateKey = rsa.ExportParameters(true);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                }
            }
        }

        internal static async Task SendRSAToClientAsync(WebSocket client)
        {
            _ = Logger.LogAsync("Sending the public key (RSA) to the client");

            var payload = new
            {
                code = OpCode.SendClientRSA,
                publicKey = _publicKey,
                modulus = Convert.ToBase64String(_publicKey.Modulus!),
                exponent = Convert.ToBase64String(_publicKey.Exponent!),
            };
            var jsonString = JsonSerializer.Serialize(payload);
            await Server.SendPayloadAsync(client, jsonString, EncryptionMode.None);
        }

        internal static async Task SaveClientsAes(WebSocket client, JsonElement root)
        {
            _ = Logger.LogAsync("Received the aes from the client");
            var aes = Aes.Create();
            aes.Key = root.GetProperty("key").GetBytesFromBase64();
            aes.IV = root.GetProperty("iv").GetBytesFromBase64();
            if (!_clientsAes.TryAdd(client, aes))
            {
                await Logger.LogAsync("Couldn´t add the client to the dict.");
                await client.CloseAsync(WebSocketCloseStatus.InternalServerError, "The Server had an problem. Try to reconnect!", CancellationToken.None);
                return;
            }

            var payload = new
            {
                code = OpCode.ReadyToReceive,
            };
            var jsonString = JsonSerializer.Serialize(payload);
            await Server.SendPayloadAsync(client, jsonString);
        }

        #region Encryption

        internal static async Task<byte[]> EncryptAes(WebSocket client, string dataToEncrypt)
        {
            if (_clientsAes.TryGetValue(client, out Aes? aes))
            {
                ICryptoTransform encryptor = aes.CreateEncryptor();

                using var ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(dataToEncrypt);
                    sw.Flush();
                }

                return ms.ToArray();
            }
            else
            {
                await client.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "The Aes is missing. Try to reconnect!", CancellationToken.None);
                return [];
            }
        }

        #endregion

        #region Decryption

        internal static JsonElement? DecryptMessage(WebSocket client, string messageToDecrypt)
        {
            var dataAsBytes = Convert.FromBase64String(messageToDecrypt);
            try
            {
                return JsonDocument.Parse(dataAsBytes).RootElement;
            }
            catch (Exception)
            {
                try
                {
                    return DecryptRSA(dataAsBytes);
                }
                catch (Exception)
                {
                    return DecryptAes(client, dataAsBytes);
                }
            }
        }

        private static JsonElement DecryptRSA(byte[] dataToDecrypt)
        {
            using var rsa = RSA.Create();
            {
                rsa.ImportParameters(_privateKey);
                var decryptedBytes = rsa.Decrypt(dataToDecrypt, RSAEncryptionPadding.Pkcs1);
                var decryptedString = Encoding.UTF8.GetString(decryptedBytes);
                return JsonDocument.Parse(decryptedString).RootElement;
            }
        }

        private static JsonElement? DecryptAes(WebSocket client, byte[] dataToDecrypt)
        {
            try
            {
                if (_clientsAes.TryGetValue(client, out Aes? aes))
                {
                    using ICryptoTransform decryptor = aes.CreateDecryptor();

                    using var memoryStream = new MemoryStream(dataToDecrypt);
                    using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
                    using var resultStream = new MemoryStream();

                    cryptoStream.CopyTo(resultStream);
                    var decryptedData = resultStream.ToArray();

                    return JsonSerializer.Deserialize<JsonElement>(decryptedData);
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                return null;
            }
        }


        #endregion
    }
}
