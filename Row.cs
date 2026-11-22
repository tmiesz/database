using System.Text;

class Row
{
    const int ID_SIZE = 4;
    const int USERNAME_SIZE = 32;
    const int EMAIL_SIZE = 255;

    const int ID_OFFSET = 0;
    const int USERNAME_OFFSET = ID_OFFSET + ID_SIZE;
    const int EMAIL_OFFSET = USERNAME_OFFSET + USERNAME_SIZE;
    public const int ROW_SIZE = ID_SIZE + USERNAME_SIZE + EMAIL_SIZE;

    public uint Id { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }

    private Row(uint id, string userName, string email)
    {
        Id = id;
        UserName = userName;
        Email = email;
    }

    public void Serialize(byte[] buffer, int start)
    {
        Array.Clear(buffer, start, ROW_SIZE);

        var idBytes = BitConverter.GetBytes(Id);
        Array.Copy(idBytes, 0, buffer, start, idBytes.Length);

        var userNameBytes = Encoding.UTF8.GetBytes(UserName);
        Array.Copy(userNameBytes, 0, buffer, start + USERNAME_OFFSET, userNameBytes.Length);

        var emailBytes = Encoding.UTF8.GetBytes(Email);
        Array.Copy(emailBytes, 0, buffer, start + EMAIL_OFFSET, emailBytes.Length);
    }

    public static Row Deserialize(byte[] buffer, int start)
    {
        var id = BitConverter.ToUInt32(buffer.AsSpan(start + ID_OFFSET, ID_SIZE));

        var nameSlot = buffer.AsSpan(start + USERNAME_OFFSET, USERNAME_SIZE);
        var nameZero = nameSlot.IndexOf((byte)0);
        var nameLength = nameZero == -1 ? USERNAME_SIZE : nameZero;
        var userName = Encoding.UTF8.GetString(nameSlot[..nameLength]);

        var emailSlot = buffer.AsSpan(start + EMAIL_OFFSET, EMAIL_SIZE);
        var emailZero = emailSlot.IndexOf((byte)0);
        var emailLength = emailZero == -1 ? EMAIL_SIZE : emailZero;
        var email = Encoding.UTF8.GetString(emailSlot[..emailLength]);

        var row = new Row(id, userName, email);

        return row;
    }

    public static bool TryCreate(uint id, string userName, string email, out Row row)
    {
        row = null!;

        if (Encoding.UTF8.GetByteCount(userName) > USERNAME_SIZE)
            return false;
        if (Encoding.UTF8.GetByteCount(email) > EMAIL_SIZE)
            return false;

        row = new Row(id, userName, email);

        return true;
    }

    public void PrintRow()
    {
        Console.WriteLine($"({Id}, {UserName}, {Email})");
    }
}

