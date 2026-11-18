class InternalNode(Page page) : Node(page)
{
    private static int CellStart(int cell) => Constants.INTERNAL_NODE_HEADER_SIZE + (cell * Constants.INTERNAL_NODE_CELL_SIZE);

    public uint NumKeys
    {
        get => BitConverter.ToUInt32(Page.Data, Constants.INTERNAL_NODE_NUM_KEYS_OFFSET);
        set
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, Page.Data, Constants.INTERNAL_NODE_NUM_KEYS_OFFSET, bytes.Length);
        }
    }
    public int RightChild
    {
        get => BitConverter.ToInt32(Page.Data, Constants.INTERNAL_NODE_RIGHT_CHILD_OFFSET);
        set
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, Page.Data, Constants.INTERNAL_NODE_RIGHT_CHILD_OFFSET, bytes.Length);
        }
    }

    public void Initialize()
    {
        NodeType = NodeType.Internal;
        IsRoot = false;
        NumKeys = 0;
        RightChild = Constants.INVALID_PAGE_NUMBER;
    }

    public uint GetKey(int cell) =>
        BitConverter.ToUInt32(Page.Data, CellStart(cell) + Constants.INTERNAL_NODE_KEY_OFFSET);

    public void SetKeyForCell(int cell, uint key)
    {
        var bytes = BitConverter.GetBytes(key);
        Array.Copy(bytes, 0, Page.Data, CellStart(cell) + Constants.INTERNAL_NODE_KEY_OFFSET, bytes.Length);
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

    public int FindChildNumber(uint key)
    {
        var min = 0;
        var onePastMax = (int)NumKeys;

        while (min != onePastMax)
        {
            var middle = (min + onePastMax) / 2;
            var keyToRight = GetKey(middle);

            if (keyToRight >= key)
                onePastMax = middle;
            else
                min = middle + 1;
        }

        return min;
    }

    public void UpdateKey(uint oldKey, uint newKey)
    {
        var childNumber = FindChildNumber(oldKey);
        if (childNumber != NumKeys)
            SetKeyForCell(childNumber, newKey);
    }

    public void CopyCell(int fromCell, int toCell)
    {
        Array.Copy(Page.Data, CellStart(fromCell), Page.Data, CellStart(toCell), Constants.INTERNAL_NODE_CELL_SIZE);
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

