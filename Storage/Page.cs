internal class Page(int pageNumber, byte[] data)
{
    public int PageNumber { get; } = pageNumber;
    public byte[] Data { get; } = data;
    public bool IsDirty { get; private set; }

    public void MarkDirty() => IsDirty = true;
    public void ClearDirty() => IsDirty = false;
}
