using System.Buffers.Binary;

internal class Header(Page page)
{
    private Page Page { get; } = page;

    public bool IsValid
        => BinaryPrimitives.ReadUInt32LittleEndian(Page.Data.AsSpan(Constants.DB_HEADER_MAGIC_OFFSET)) == Constants.DB_HEADER_MAGIC;

    public void Initialize()
    {
        Page.MarkDirty();
        BinaryPrimitives.WriteUInt32LittleEndian(
            Page.Data.AsSpan(Constants.DB_HEADER_MAGIC_OFFSET),
            Constants.DB_HEADER_MAGIC
        );
        BinaryPrimitives.WriteUInt32LittleEndian(
            Page.Data.AsSpan(Constants.DB_HEADER_COUNT_OFFSET),
            Constants.TREE_COUNT
        );
        for (int i = 0; i < Constants.TREE_COUNT; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                Page.Data.AsSpan(Constants.RootSlotOffset(i)),
                Constants.INVALID_PAGE_NUMBER
            );
        }

    }

    public int GetRoot(int tree)
    {
        ValidateTree(tree);
        return BinaryPrimitives.ReadInt32LittleEndian(Page.Data.AsSpan(Constants.RootSlotOffset(tree)));
    }

    public void SetRoot(int tree, int pageNumber)
    {
        ValidateTree(tree);
        Page.MarkDirty();
        BinaryPrimitives.WriteInt32LittleEndian(Page.Data.AsSpan(Constants.RootSlotOffset(tree)), pageNumber);
    }

    private static void ValidateTree(int tree)
    {
        if (tree < 0 || tree >= Constants.TREE_COUNT)
            throw new ArgumentOutOfRangeException(nameof(tree));
    }
}
