using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using ZstdNet;

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

        #region Compression/Decompression

        /// <summary>
        /// <c>IF</c> the compress data is smaller than the original data: <c>returns</c> compressed data
        /// <c>ELSE returns</c> the original data
        /// </summary>
        internal static byte[] CompressData(byte[] data)
        {
            using var compressor = new Compressor(new CompressionOptions(1));
            var compressedData = compressor.Wrap(data);
            return compressedData.Length >= data.Length
                ? data
                : compressedData;
        }

        internal static byte[] DecompressData(byte[] data)
        {
            try
            {
                using var decompressor = new Decompressor();
                return decompressor.Unwrap(data);
            }
            catch (Exception)
            {
                return data;
            }
        }

        #endregion

        #region Encryption

        internal static async Task<byte[]> EncryptAes(WebSocket client, byte[] dataToEncrypt)
        {
            if (_clientsAes.TryGetValue(client, out Aes? aes))
            {
                ICryptoTransform encryptor = aes.CreateEncryptor();

                using var ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    await cs.WriteAsync(dataToEncrypt);
                    await cs.FlushAsync();
                }

                return ms.ToArray();
            }
            else
            {
                await client.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "The AES key is missing. Try to reconnect!", CancellationToken.None);
                return Array.Empty<byte>();
            }
        }


        #endregion

        #region Decryption

        internal static JsonElement? DecryptMessage(WebSocket client, string messageToDecrypt)
        {
            var dataAsBytes = Convert.FromBase64String(messageToDecrypt);
            try
            {
                var decompressedData = DecompressData(dataAsBytes);
                return JsonDocument.Parse(decompressedData).RootElement;
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
                var decompressedData = DecompressData(decryptedBytes);
                return JsonDocument.Parse(decompressedData).RootElement;
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
                    var decompressedData = DecompressData(decryptedData);

                    return JsonSerializer.Deserialize<JsonElement>(decompressedData);
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
