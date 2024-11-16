namespace ServerMessengerHttp
{
    internal class User
    {
        internal DateOnly BirthDate { get; set; }
        internal string Username { get; set; } = string.Empty;
        internal string Email { get; set; } = string.Empty;
        internal string Password { get; set; } = string.Empty;

        public override bool Equals(object? obj)
        {
            return obj is User otherUser
                && Email == otherUser.Email
                && Username == otherUser.Username;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Email, Username);
        }

        public static bool operator ==(User? left, User? right)
        {
            if (left is null)
                return right is null;

            if (right is null)
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(User? left, User? right)
        {
            return !(left == right);
        }
    }
}
