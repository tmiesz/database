internal class Btree(Pager pager, TreeLayout layout, int rootPageNumber)
{
    public Pager Pager { get; } = pager;
    public TreeLayout Layout { get; } = layout;
    public int RootPageNumber { get; } = rootPageNumber;

    public InsertOutcome Insert(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        var cursor = Cursor.CreateAtKey(this, key);
        var leaf = cursor.Leaf;

        var cellNumber = cursor.CellNumber;
        if (cellNumber < leaf.CellCount && leaf.GetKey((int)cellNumber).SequenceEqual(key))
            return InsertOutcome.Duplicate;

        if (leaf.CellCount >= Layout.LeafMaxCells)
        {
            SplitAndInsert(cursor, key, value);
            return InsertOutcome.Success;
        }

        leaf.MakeRoomAt((int)cellNumber);
        leaf.SetKeyForCell((int)cellNumber, key);
        leaf.SetValueForCell((int)cellNumber, value);

        leaf.CellCount++;

        return InsertOutcome.Success;
    }

    public void PrintTree(int pageNumber, int indentationLevel, KeyFormatter format)
    {
        var page = Pager.RetrievePage(pageNumber);

        var nodeType = Node.GetNodeType(page);

        switch (nodeType)
        {
            case NodeType.Internal:
                var node = GetInternalNode(page);
                Indent(indentationLevel);
                Console.WriteLine($"- internal (size {node.NumKeys})");
                if (node.NumKeys > 0)
                {
                    for (int i = 0; i < node.NumKeys; i++)
                    {
                        PrintTree(node.GetChild(i), indentationLevel + 1, format);
                        Indent(indentationLevel + 1);
                        Console.WriteLine($"- key {format(node.GetKey(i))}");
                    }
                    PrintTree(node.RightChild, indentationLevel + 1, format);
                }
                break;
            case NodeType.Leaf:
                var leaf = GetLeafNode(page);
                Indent(indentationLevel);
                Console.WriteLine($"- leaf (size {leaf.CellCount})");
                for (int i = 0; i < leaf.CellCount; i++)
                {
                    Indent(indentationLevel + 1);
                    Console.WriteLine($"- {format(leaf.GetKey(i))}");
                }
                break;
            default:
                Console.WriteLine("Unexpected node type.");
                Environment.Exit(1);
                break;
        }
    }

    public void InitializeEmpty()
    {
        var node = GetLeafNode(RootPageNumber);
        node.Initialize();
        node.IsRoot = true;
    }

    public LeafNode GetLeafNode(Page page) =>
        new(page, Layout);

    public LeafNode GetLeafNode(int pageNumber) =>
        GetLeafNode(Pager.RetrievePage(pageNumber));

    public InternalNode GetInternalNode(Page page) =>
        new(page, Layout);

    public InternalNode GetInternalNode(int pageNumber) =>
        GetInternalNode(Pager.RetrievePage(pageNumber));

    private void CreateNewRoot(int rightChildPageNumber)
    {
        var rootPage = Pager.RetrievePage(RootPageNumber);

        var rightChildPage = Pager.RetrievePage(rightChildPageNumber);
        var rootIsInternal = Node.GetNodeType(rootPage) == NodeType.Internal;

        if (rootIsInternal)
            GetInternalNode(rightChildPage).Initialize();

        var leftChildPageNumber = Pager.GetUnusedPageNumber();
        var leftChildPage = Pager.RetrievePage(leftChildPageNumber);

        leftChildPage.MarkDirty();
        Array.Copy(rootPage.Data, leftChildPage.Data, Constants.PAGE_SIZE);

        if (rootIsInternal)
        {
            var leftNode = GetInternalNode(leftChildPage);

            for (int i = 0; i < leftNode.NumKeys; i++)
            {
                var childPageNumber = leftNode.GetChild(i);
                Node.SetParent(Pager.RetrievePage(childPageNumber), leftChildPageNumber);
            }

            Node.SetParent(Pager.RetrievePage(leftNode.RightChild), leftChildPageNumber);
        }

        Node.SetIsRoot(leftChildPage, false);
        Node.SetParent(leftChildPage, RootPageNumber);
        Node.SetParent(rightChildPage, RootPageNumber);

        var root = GetInternalNode(rootPage);
        root.Initialize();
        root.IsRoot = true;
        root.NumKeys = 1;
        root.SetChild(0, leftChildPageNumber);
        root.SetKeyForCell(0, GetNodeMaxKey(leftChildPage));
        root.RightChild = rightChildPageNumber;
    }

    private void InternalNodeInsert(int parentPageNumber, int childPageNumber)
    {
        var parent = GetInternalNode(Pager.RetrievePage(parentPageNumber));
        var child = Pager.RetrievePage(childPageNumber);
        var childMaxKey = GetNodeMaxKey(child);
        var index = parent.FindChildNumber(childMaxKey);
        var originalNumKeys = parent.NumKeys;

        if (originalNumKeys >= Layout.InternalMaxCells)
        {
            InternalNodeSplitAndInsert(parentPageNumber, childPageNumber);
            return;
        }

        var rightChildPageNumber = parent.RightChild;

        if (rightChildPageNumber == Constants.INVALID_PAGE_NUMBER)
        {
            parent.RightChild = childPageNumber;
            return;
        }

        parent.NumKeys = originalNumKeys + 1;
        var rightChild = Pager.RetrievePage(rightChildPageNumber);
        var rightChildMaxKey = GetNodeMaxKey(rightChild);

        if (childMaxKey.AsSpan().SequenceCompareTo(rightChildMaxKey) > 0)
        {
            parent.SetChild((int)originalNumKeys, rightChildPageNumber);
            parent.SetKeyForCell((int)originalNumKeys, rightChildMaxKey);
            parent.RightChild = childPageNumber;
        }
        else
        {
            for (int i = (int)originalNumKeys; i > index; i--)
                parent.CopyCell(i - 1, i);

            parent.SetChild(index, childPageNumber);
            parent.SetKeyForCell(index, childMaxKey);
        }
    }

    private void InternalNodeSplitAndInsert(int parentPageNumber, int childPageNumber)
    {
        var oldPageNumber = parentPageNumber;
        var oldPage = Pager.RetrievePage(oldPageNumber);
        var oldNode = GetInternalNode(oldPage);
        var oldMax = GetNodeMaxKey(oldPage);

        var childPage = Pager.RetrievePage(childPageNumber);
        var childMax = GetNodeMaxKey(childPage);

        var newPageNumber = Pager.GetUnusedPageNumber();

        var splittingRoot = oldNode.IsRoot;

        InternalNode parent;

        if (splittingRoot)
        {
            CreateNewRoot(newPageNumber);
            parent = GetInternalNode(Pager.RetrievePage(RootPageNumber));
            oldPageNumber = parent.GetChild(0);
            oldNode = GetInternalNode(Pager.RetrievePage(oldPageNumber));
        }
        else
        {
            parent = GetInternalNode(Pager.RetrievePage(oldNode.Parent));
            GetInternalNode(Pager.RetrievePage(newPageNumber)).Initialize();
        }

        var currentPageNumber = oldNode.RightChild;
        InternalNodeInsert(newPageNumber, currentPageNumber);
        Node.SetParent(Pager.RetrievePage(currentPageNumber), newPageNumber);
        oldNode.RightChild = Constants.INVALID_PAGE_NUMBER;

        for (int i = Layout.InternalMaxCells - 1; i > Layout.InternalMaxCells / 2; i--)
        {
            currentPageNumber = oldNode.GetChild(i);
            InternalNodeInsert(newPageNumber, currentPageNumber);
            Node.SetParent(Pager.RetrievePage(currentPageNumber), newPageNumber);
            oldNode.NumKeys--;
        }

        oldNode.RightChild = oldNode.GetChild((int)oldNode.NumKeys - 1);
        oldNode.NumKeys--;

        var maxAfterSplit = GetNodeMaxKey(Pager.RetrievePage(oldPageNumber));
        var destinationPageNumber = childMax.AsSpan().SequenceCompareTo(maxAfterSplit) < 0 ? oldPageNumber : newPageNumber;

        InternalNodeInsert(destinationPageNumber, childPageNumber);
        Node.SetParent(childPage, destinationPageNumber);

        var newMax = GetNodeMaxKey(Pager.RetrievePage(oldPageNumber));
        parent.UpdateKey(oldMax, newMax);

        if (!splittingRoot)
        {
            var upperPageNumber = oldNode.Parent;
            Node.SetParent(Pager.RetrievePage(newPageNumber), upperPageNumber);
            InternalNodeInsert(upperPageNumber, newPageNumber);
        }
    }

    private byte[] GetNodeMaxKey(Page page)
    {
        var nodeType = Node.GetNodeType(page);
        if (nodeType is NodeType.Leaf)
            return GetLeafNode(page).MaxKey;

        var rightChildPageNumber = GetInternalNode(page).RightChild;
        return GetNodeMaxKey(Pager.RetrievePage(rightChildPageNumber));
    }


    private void SplitAndInsert(Cursor cursor, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        var oldLeaf = cursor.Leaf;
        var oldMax = oldLeaf.MaxKey;
        var newPageNumber = Pager.GetUnusedPageNumber();

        var page = Pager.RetrievePage(newPageNumber);
        var newLeaf = GetLeafNode(page);

        newLeaf.Initialize();
        newLeaf.NextLeaf = oldLeaf.NextLeaf;
        oldLeaf.NextLeaf = newPageNumber;
        newLeaf.Parent = oldLeaf.Parent;

        int insertCell = (int)cursor.CellNumber;

        LeafNode destination;
        int cellInDestination;
        for (int i = Layout.LeafMaxCells; i >= 0; i--)
        {

            if (i >= Layout.LeafLeftSplitCount)
                destination = newLeaf;
            else destination = oldLeaf;

            cellInDestination = i % Layout.LeafLeftSplitCount;

            if (i == insertCell)
            {
                destination.SetKeyForCell(cellInDestination, key);
                destination.SetValueForCell(cellInDestination, value);
            }
            else if (i > insertCell)
            {
                oldLeaf.CopyCellTo(i - 1, destination, cellInDestination);
            }
            else
            {
                oldLeaf.CopyCellTo(i, destination, cellInDestination);
            }
        }

        oldLeaf.CellCount = (uint)Layout.LeafLeftSplitCount;
        newLeaf.CellCount = (uint)Layout.LeafRightSplitCount;

        if (oldLeaf.IsRoot)
            CreateNewRoot(newPageNumber);
        else
        {
            var parentPageNumber = oldLeaf.Parent;
            var newMax = oldLeaf.MaxKey;
            var parent = GetInternalNode(parentPageNumber);

            parent.UpdateKey(oldMax, newMax);
            InternalNodeInsert(parentPageNumber, newPageNumber);
        }
    }

    private static void Indent(int level)
    {
        for (int i = 0; i < level; i++)
            Console.Write("  ");
    }
}

delegate string KeyFormatter(ReadOnlySpan<byte> key);
