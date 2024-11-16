using Npgsql;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace ServerMessengerHttp
{
    internal static class UserDatabase
    {
        internal static readonly ConcurrentDictionary<WebSocket, (User user, int verificationCode)> _usersTryingToCreateAcc = [];
        private static string _connectionString = "";
#pragma warning disable CS8618
        private static Aes _aes;
#pragma warning restore CS8618

        internal static void Init()
        {
            _connectionString = ReadConnectionString();
            if (!IsDatabaseOnline()) Server.StopServer("Database is offline");
            DeriveKeyAndIV();
        }

        private static string ReadConnectionString()
        {
            var reader = new StreamReader(@"C:\Users\Crist\Desktop\PostgreDatabaseMessenger.txt");
            return reader.ReadToEnd();
        }

        private static bool IsDatabaseOnline()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            try
            {
                conn.Open();
                return true;
            }
            catch (NpgsqlException)
            {
                return false;
            }
        }

        private static void DeriveKeyAndIV()
        {
            using var streamReader = new StreamReader(@"C:\Users\Crist\Desktop\AESData.txt");
            var password = streamReader.ReadLine()!;
            var salt = Encoding.UTF8.GetBytes(streamReader.ReadLine()!);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
            _aes = Aes.Create();
            _aes.Key = pbkdf2.GetBytes(32);
            _aes.IV = pbkdf2.GetBytes(16);
        }

        private static string EncryptDataAESDatabase(byte[] data)
        {
            using ICryptoTransform encryptor = _aes.CreateEncryptor();
            using var msEncrypt = new MemoryStream();
            using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);

            csEncrypt.Write(data, 0, data.Length);
            csEncrypt.FlushFinalBlock();

            return Convert.ToBase64String(msEncrypt.ToArray());
        }

        private static string DecryptDataAES(byte[] data)
        {
            using ICryptoTransform decryptor = _aes.CreateDecryptor();

            using var msDecrypt = new MemoryStream(data);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);

            return srDecrypt.ReadToEnd();
        }

        internal static async Task<(bool emailInUse, bool usernameInUse)?> EmailOrUsernameInDatabase(string email, string username)
        {
            try
            {
                var command = "SELECT email, username FROM userinfo WHERE email = @e OR username = @u";
                using var conn = new NpgsqlConnection(_connectionString);
                var cmd = new NpgsqlCommand(command, conn);

                var encryptedEmail = EncryptDataAESDatabase(Encoding.UTF8.GetBytes(email));
                var encryptedUsername = EncryptDataAESDatabase(Encoding.UTF8.GetBytes(username));

                cmd.Parameters.AddWithValue("@e", encryptedEmail);
                cmd.Parameters.AddWithValue("@u", encryptedUsername);

                await conn.OpenAsync();
                using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var emailInUse = reader.GetString(0) == encryptedEmail;
                    var usernameInUse = reader.GetString(1) == encryptedUsername;

                    return (emailInUse, usernameInUse);
                }
                return (false, false);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                return null;
            }
        }

        internal static async Task<bool> PutUserIntoDb(User user)
        {
            try
            {
                var command = "INSERT INTO userinfo (email, username, birthdate, password, profilpic) VALUES (@e, @u, @b, @pw, @p)";

                using var conn = new NpgsqlConnection(_connectionString);

                var encryptedEmail = EncryptDataAESDatabase(Encoding.UTF8.GetBytes(user.Email));
                var encryptedUsername = EncryptDataAESDatabase(Encoding.UTF8.GetBytes(user.Username));
                var encryptedPassword = EncryptDataAESDatabase(Encoding.UTF8.GetBytes(user.Password));

                var cmd = new NpgsqlCommand(command, conn);
                cmd.Parameters.AddWithValue("@e", encryptedEmail);
                cmd.Parameters.AddWithValue("@u", encryptedUsername);
                cmd.Parameters.AddWithValue("@b", user.BirthDate);
                cmd.Parameters.AddWithValue("@pw", encryptedPassword);
                cmd.Parameters.AddWithValue("@p", File.ReadAllBytes(@"C:\Users\Crist\source\repos\ServerMessengerHttp\ServerMessengerHttp\NeededFiles\ProfilPic.png"));

                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                return false;
            }
        }
    }
}
