using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ServerMessengerHttp
{
    internal static class Server
    {
        internal static void StartServer()
        {
            _ = Logger.LogAsync($"Starting the Server.");
            UserDatabase.Init();
            Task.Run(AcceptClientsAsync);
        }

        internal static void StopServer(string errorMessage)
        {
            _ = Logger.LogAsync("Stopping the server");
            throw new Exception(errorMessage);
        }

        internal static async Task AcceptClientsAsync()
        {
            HttpListener listener = new();
            listener.Prefixes.Add("http://127.0.0.1:5000/");
            listener.Start();

            while (listener.IsListening)
            {
                HttpListenerContext context = await listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    _ = Logger.LogAsync("Accepting a client");
                    HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
                    WebSocket client = webSocketContext.WebSocket;

                    _ = Task.Run(() => HandleClientAsync(client));
                    await Security.SendRSAToClientAsync(client);
                }
                else
                {
                    throw new Exception("Request wasn´t a Websocket request");
                }
            }
        }

        internal static async Task HandleClientAsync(WebSocket client)
        {
            var buffer = new byte[65536];
            CancellationToken cancellationToken = new CancellationTokenSource().Token;
            var completeMessage = new StringBuilder();
            while (client.State == WebSocketState.Open)
            {
                try
                {
                    WebSocketReceiveResult receivedData = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    _ = Logger.LogAsync($"RECEIVED: The received payload is {receivedData.Count} bytes long");

                    if (receivedData.CloseStatus is WebSocketCloseStatus closeStatus)
                    {
                        await HandleClosingMessage(closeStatus, receivedData.CloseStatusDescription!);
                        break;
                    }

                    var receivedDataAsString = Convert.ToBase64String(buffer, 0, receivedData.Count);
                    completeMessage.Append(receivedDataAsString);
                    JsonElement? message;

                    if (receivedData.EndOfMessage)
                    {
                        var completePayload = completeMessage.ToString();
                        completeMessage.Clear();
                        message = Security.DecryptMessage(client, completePayload);

                        if (message is JsonElement root)
                        {
                            _ = Logger.LogAsync($"RECEIVED: {root}");
                            await HandleMessageAsync(client, root.GetProperty("code").GetOpCode(), root);
                        }
                        else
                        {
                            await client.CloseAsync
                                (WebSocketCloseStatus.InternalServerError,
                                "The Server had an internal problem. Try to reconnect!",
                                CancellationToken.None);
                            break;
                        }
                    }
                    else
                    {
                        _ = Logger.LogAsync("The message is being sent in parts. Waiting for the next part");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                }
            }
            CleanUpClosedConnection(client);
        }

        private static async Task HandleClosingMessage(WebSocketCloseStatus socketCloseStatus, string reason)
        {
            await Logger.LogAsync([$"The server closed the connection.", $"Close status: {socketCloseStatus}", $"Reason: {reason}"]);
        }

        private static async Task HandleMessageAsync(WebSocket client, OpCode opCode, JsonElement root)
        {
            switch (opCode)
            {
                case OpCode.ReceiveAes:
                    await Security.SaveClientsAes(client, root);
                    break;
                case OpCode.ReceiveRequestToCreateAcc:
                    await HandleClientMessages.HandleCreateAccountRequest(client, root);
                        break;
                case OpCode.VerificationProcess:
                    await HandleClientMessages.VerificationProcess(client, root);
                    break;
            }
        }

        private static void CleanUpClosedConnection(WebSocket client)
        {
            _ = Logger.LogAsync($"Lost the connection with a client");
            Security._clientsAes.TryRemove(client, out _);
            UserDatabase._usersTryingToCreateAcc.TryRemove(client, out _);
            client?.Dispose();
        }

        internal static async Task SendPayloadAsync(WebSocket client, string payload, EncryptionMode encryptionMode = EncryptionMode.Aes)
        {
            _ = Logger.LogAsync($"SENDING({encryptionMode}): {payload}");
            ArgumentNullException.ThrowIfNull(payload);
            if (client.State != WebSocketState.Open)
            {
                CleanUpClosedConnection(client);
                return;
            }

            var buffer = encryptionMode == EncryptionMode.None
                ? Encoding.UTF8.GetBytes(payload)
                : await Security.EncryptAes(client, payload);

            if (buffer.Length == 0)
            {
                CleanUpClosedConnection(client);
                return;
            }

            var bufferLengthOfClient = 65536;
            var parts = (int)Math.Ceiling((double)buffer.Length / bufferLengthOfClient);

            if (parts > 1)
            {
                var partedBuffer = buffer.Chunk(bufferLengthOfClient).ToArray();
                for (var i = 0; i < partedBuffer.Length; i++)
                {
                    var item = partedBuffer[i];
                    var endOfMessage = i == partedBuffer.Length - 1;

                    await client.SendAsync(new ArraySegment<byte>(item), WebSocketMessageType.Binary, endOfMessage, CancellationToken.None);
                }
            }
            else
            {
                await client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
        }
    }
}
