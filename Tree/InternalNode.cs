internal class InternalNode(Page page, TreeLayout layout) : Node(page)
{
    private int CellStart(int cell) => Constants.INTERNAL_NODE_HEADER_SIZE + (cell * Layout.InternalCellSize);

    public uint NumKeys
    {
        get => BitConverter.ToUInt32(Page.Data, Constants.INTERNAL_NODE_NUM_KEYS_OFFSET);
        set
        {
            Page.MarkDirty();
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, Page.Data, Constants.INTERNAL_NODE_NUM_KEYS_OFFSET, bytes.Length);
        }
    }
    public int RightChild
    {
        get => BitConverter.ToInt32(Page.Data, Constants.INTERNAL_NODE_RIGHT_CHILD_OFFSET);
        set
        {
            Page.MarkDirty();
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, Page.Data, Constants.INTERNAL_NODE_RIGHT_CHILD_OFFSET, bytes.Length);
        }
    }

    private TreeLayout Layout { get; } = layout;

    public void Initialize()
    {
        NodeType = NodeType.Internal;
        IsRoot = false;
        NumKeys = 0;
        RightChild = Constants.INVALID_PAGE_NUMBER;
    }

    public ReadOnlySpan<byte> GetKey(int cell) =>
        Page.Data.AsSpan(CellStart(cell) + Constants.INTERNAL_NODE_KEY_OFFSET, Layout.KeySize);

    public void SetKeyForCell(int cell, ReadOnlySpan<byte> key)
    {
        if (key.Length != Layout.KeySize)
            throw new ArgumentException("Key length does not match the layout.", nameof(key));

        Page.MarkDirty();
        key.CopyTo(Page.Data.AsSpan(CellStart(cell) + Constants.INTERNAL_NODE_KEY_OFFSET, Layout.KeySize));
    }

    public int GetChild(int childNumber)
    {
        ValidateChildNumber(childNumber);
        var child = childNumber == NumKeys ? RightChild : BitConverter.ToInt32(Page.Data, CellStart(childNumber));

        if (child == Constants.INVALID_PAGE_NUMBER)
        {
            Console.WriteLine($"Tried to access child number {childNumber}, but it is an invalid page.");
            Environment.Exit(1);
        }
        return child;
    }

    public void SetChild(int childNumber, int pageNumber)
    {
        Page.MarkDirty();
        ValidateChildNumber(childNumber);

        if (childNumber == NumKeys)
        {
            RightChild = pageNumber;
        }
        else
        {
            var bytes = BitConverter.GetBytes(pageNumber);
            Array.Copy(bytes, 0, Page.Data, CellStart(childNumber), bytes.Length);
        }
    }

    public int FindChildNumber(ReadOnlySpan<byte> key)
    {
        var min = 0;
        var onePastMax = (int)NumKeys;

        while (min != onePastMax)
        {
            var middle = (min + onePastMax) / 2;

            if (GetKey(middle).SequenceCompareTo(key) >= 0)
                onePastMax = middle;
            else min = middle + 1;
        }

        return min;
    }

    public void UpdateKey(ReadOnlySpan<byte> oldKey, ReadOnlySpan<byte> newKey)
    {
        var childNumber = FindChildNumber(oldKey);
        if (childNumber != NumKeys)
            SetKeyForCell(childNumber, newKey);
    }

    public void CopyCell(int fromCell, int toCell)
    {
        Page.MarkDirty();
        Array.Copy(Page.Data, CellStart(fromCell), Page.Data, CellStart(toCell), Layout.InternalCellSize);
    }

    private void ValidateChildNumber(int childNumber)
    {
        if (childNumber > NumKeys)
        {
            Console.WriteLine($"Tried to access child number {childNumber} > num keys {NumKeys}");
            Environment.Exit(1);
        }
    }
}

