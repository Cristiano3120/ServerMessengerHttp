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
            Task.Run(AcceptClients);
        }

        internal static async Task AcceptClients()
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

                    _ = Task.Run(() => HandleClient(client));
                    await Security.SendRSAToClient(client);
                }
                else
                {
                    throw new Exception("Request wasn´t a Websocket request");
                }
            }
        }

        internal static async Task HandleClient(WebSocket client)
        {
            var buffer = new byte[65536];
            CancellationToken cancellationToken = new CancellationTokenSource().Token;
            var completeMessage = new StringBuilder();
            while (client.State == WebSocketState.Open)
            {
                try
                {
                    WebSocketReceiveResult receivedData = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    Console.WriteLine(receivedData.Count);
                    var receivedDataAsString = Encoding.UTF8.GetString(buffer, 0, receivedData.Count);
                    completeMessage.Append(receivedDataAsString);
                    JsonElement message;

                    if (receivedData.EndOfMessage)
                    {
                        message = JsonDocument.Parse(completeMessage.ToString()).RootElement;
                        _ = Logger.LogAsync(message.ToString());
                        completeMessage.Clear();
                        HandleMessage(message.GetOpCode(), message);
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
            CloseConnectionToClient(client);
        }

        private static void HandleMessage(OpCode opCode, JsonElement root)
        {

        }

        private static void CloseConnectionToClient(WebSocket client)
        {
            _ = Logger.LogAsync($"Lost the connection with a client");
            client.Dispose();
        }

        internal static async Task SendPayloadAsync(WebSocket client, string payload, EncryptionMode encryptionMode = EncryptionMode.Aes, bool endOfMessage = true)
        {
            _ = Logger.LogAsync($"Sending: {payload}");
            ArgumentNullException.ThrowIfNull(payload);
            if (client.State != WebSocketState.Open)
            {
                CloseConnectionToClient(client);
                return;
            }

            var buffer = encryptionMode == EncryptionMode.None
                ? Encoding.UTF8.GetBytes(payload)
                : throw new NotImplementedException("Encrypt data with Aes");

            await client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, endOfMessage, CancellationToken.None);
        }
    }
}
