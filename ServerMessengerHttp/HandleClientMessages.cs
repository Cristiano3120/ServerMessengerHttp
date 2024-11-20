using System.Net.Mail;
using System.Net;
using System.Net.WebSockets;
using System.Text.Json;

namespace ServerMessengerHttp
{
    internal static class HandleClientMessages
    {
        private static readonly string _emailAppPaswword;

        static HandleClientMessages()
        {
            using var streamReader = new StreamReader(@"C:\Users\Crist\Desktop\gmailAppPassword.txt");
            _emailAppPaswword = streamReader.ReadToEnd();
        }

        internal static async Task HandleCreateAccountRequest(WebSocket client, JsonElement root)
        {
            var user = new User()
            {
                Email = root.GetProperty("email").GetString()!,
                Password = root.GetProperty("password").GetString()!,
                Username = root.GetProperty("username").GetString()!,
                BirthDate = root.GetProperty("dateOfBirth").GetDateOnly(),
            };

            (bool, bool)? result = await UserDatabase.EmailOrUsernameInDatabase(user.Email, user.Username)!;
            if (result == null)
            {
                await client.CloseAsync(WebSocketCloseStatus.InternalServerError, "The database isn´t functional at the moment", CancellationToken.None);
                return;
            }

            (bool emailInUse, bool usernameInUse) = result.Value;
            User? userWithSameEmailOrAndUsername = UserDatabase._usersTryingToCreateAcc.FirstOrDefault(x => x.Value.user == user).Value.user;
            var jsonString = "";

            if (userWithSameEmailOrAndUsername != null || emailInUse || usernameInUse)
            {
                var payload = new
                {
                    code = OpCode.ResponseRequestToCreateAcc,
                    succesful = false,
                    email = userWithSameEmailOrAndUsername?.Email == user.Email || emailInUse,
                    username = userWithSameEmailOrAndUsername?.Username == user.Username || usernameInUse,
                };
                jsonString = JsonSerializer.Serialize(payload);
            }
            else
            {
                var verificationCode = Random.Shared.Next(100000, 999999);
                SendEmail(user, verificationCode);
                UserDatabase._usersTryingToCreateAcc.TryAdd(client, (user, verificationCode));
                var payload = new
                {
                    code = OpCode.ResponseRequestToCreateAcc,
                    sucessful = true,
                    email = user.Email,
                };
                jsonString = JsonSerializer.Serialize(payload);
            }

            await Server.SendPayloadAsync(client, jsonString);
        }

        internal static async Task VerificationProcess(WebSocket client, JsonElement root)
        {
            static async Task SendResponseAsync(WebSocket client, object payload)
            {
                var jsonString = JsonSerializer.Serialize(payload);
                await Server.SendPayloadAsync(client, jsonString);
            }

            var providedCode = root.GetProperty("verificationCode").GetInt32();
            if (!UserDatabase._usersTryingToCreateAcc.TryGetValue(client, out (User user, int verificationCode) userInfo))
            {
                await client.CloseAsync(WebSocketCloseStatus.InternalServerError, "Please restart the app", CancellationToken.None);
                return;
            }

            if (userInfo.verificationCode != providedCode)
            {
                await SendResponseAsync(client, new
                {
                    code = OpCode.VerificationResult,
                    sucessful = false,
                    error = "Wrong verification code",
                });
                return;
            }

            if (!await UserDatabase.PutUserIntoDb(userInfo.user))
            {
                await SendResponseAsync(client, new
                {
                    code = OpCode.UnexpectedError,
                    error = UnexpectedError.FailedToPutUserIntoDb,
                });
                return;
            }

            await SendResponseAsync(client, new
            {
                code = OpCode.VerificationResult,
                successful = true,
            });
        }

        private static void SendEmail(User user, int verificationCode)
        {
            _ = Logger.LogAsync("Sending an email");
            var fromAddress = "ccardoso7002@gmail.com";
            var toAddress = $"{user.Email}";
            var subject = $"Verification Email";
            var body = $"Hello {user.Username} this is your verification code: {verificationCode}." +
                $" If you did not attempt to create an account, please disregard this email.";

            var mail = new MailMessage(fromAddress, toAddress, subject, body);

            var smtpClient = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(fromAddress, _emailAppPaswword),
                EnableSsl = true
            };
            smtpClient.Send(mail);
        }
    }
}
