internal abstract class Node(Page page)
{
    protected Page Page { get; } = page;

    public int Parent
    {
        get => BitConverter.ToInt32(Page.Data, Constants.PARENT_POINTER_OFFSET);
        set => SetParent(Page, value);
    }

    public bool IsRoot
    {
        get => Page.Data[Constants.IS_ROOT_OFFSET] != 0;
        set
        {
            Page.MarkDirty();
            Page.Data[Constants.IS_ROOT_OFFSET] = value ? (byte)1 : (byte)0;
        }

    }

    public NodeType NodeType
    {
        get => (NodeType)Page.Data[Constants.NODE_TYPE_OFFSET];
        set
        {
            Page.MarkDirty();
            Page.Data[Constants.NODE_TYPE_OFFSET] = (byte)value;
        }
    }

    public static NodeType GetNodeType(Page page) =>
        (NodeType)page.Data[Constants.NODE_TYPE_OFFSET];

    public static void SetParent(Page page, int parentPageNumber)
    {
        page.MarkDirty();
        var bytes = BitConverter.GetBytes(parentPageNumber);
        Array.Copy(bytes, 0, page.Data, Constants.PARENT_POINTER_OFFSET, bytes.Length);
    }

    public static void SetIsRoot(Page page, bool isRoot)
    {
        page.MarkDirty();
        var bytes = BitConverter.GetBytes(isRoot);
        Array.Copy(bytes, 0, page.Data, Constants.IS_ROOT_OFFSET, bytes.Length);
    }
}
