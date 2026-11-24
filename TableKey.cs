using System.Buffers.Binary;

static class TableKey
{
    public const int SIZE = sizeof(uint);

    public static byte[] FromId(uint id)
    {
        var key = new byte[SIZE];
        BinaryPrimitives.WriteUInt32BigEndian(key, id);
        return key;
    }

    public static uint ToId(ReadOnlySpan<byte> key) => BinaryPrimitives.ReadUInt32BigEndian(key);
}
