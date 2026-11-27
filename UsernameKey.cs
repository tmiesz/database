using System.Buffers.Binary;
using System.Text;

static class UsernameKey
{
    public const int SIZE = Row.USERNAME_SIZE + TableKey.SIZE;

    public static byte[] Create(string username, uint id)
    {
        if (Encoding.UTF8.GetByteCount(username) > Row.USERNAME_SIZE)
            throw new ArgumentException("Username is too long.", nameof(username));

        var bytes = new byte[SIZE];

        Encoding.UTF8.GetBytes(username, bytes.AsSpan(0, Row.USERNAME_SIZE));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(Row.USERNAME_SIZE), id);

        return bytes;
    }

    public static string GetUsername(ReadOnlySpan<byte> key)
    {
        var usernameBytes = key[..Row.USERNAME_SIZE];
        var zeroIndex = usernameBytes.IndexOf((byte)0);

        if (zeroIndex >= 0)
            usernameBytes = usernameBytes[..zeroIndex];

        return Encoding.UTF8.GetString(usernameBytes);
    }

    public static uint ToId(ReadOnlySpan<byte> key)
        => BinaryPrimitives.ReadUInt32BigEndian(key.Slice(Row.USERNAME_SIZE, TableKey.SIZE));
}
