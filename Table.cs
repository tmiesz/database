class Table
{
    public Pager Pager { get; }
    public int RootPageNumber { get; } = 0;

    public Table(Pager pager)
    {
        Pager = pager;
        if (pager.PageCount == 0)
        {
            var page = pager.RetrievePage(RootPageNumber);
            var root = new LeafNode(page);
            root.Initialize();

            root.IsRoot = true;

            Pager.CompleteStatement();
        }
    }

    public InsertOutcome Insert(Row row)
    {
        var cursor = Cursor.CreateAtKey(this, row.Id);
        var leaf = cursor.Leaf;

        var cellNumber = cursor.CellNumber;
        if (cellNumber < leaf.CellCount && leaf.GetKey((int)cellNumber) == row.Id)
            return InsertOutcome.Duplicate;

        if (leaf.CellCount >= Constants.LEAF_NODE_MAX_CELLS)
        {
            SplitAndInsert(cursor, row);
            return InsertOutcome.Success;
        }

        leaf.MakeRoomAt((int)cellNumber);
        leaf.SetKeyForCell((int)cellNumber, row.Id);
        leaf.StoreRow(row, (int)cellNumber);

        leaf.CellCount++;

        return InsertOutcome.Success;
    }

    public void Select()
    {
        var cursor = Cursor.CreateAtStart(this);
        while (!cursor.EndOfTable)
        {
            var row = cursor.GetValue();
            row.PrintRow();
            cursor.Advance();
        }
    }

    public void Close()
    {
        for (int i = 0; i < Pager.PageCount; i++)
        {
            if (!Pager.IsPageLoaded(i))
                continue;
            Pager.Flush(i);
        }

        Pager.FileStream.Dispose();
    }

    public void SplitAndInsert(Cursor cursor, Row row)
    {
        var oldLeaf = cursor.Leaf;
        var oldMax = oldLeaf.MaxKey;
        var newPageNumber = Pager.GetUnusedPageNumber();

        var page = Pager.RetrievePage(newPageNumber);
        var newLeaf = new LeafNode(page);

        newLeaf.Initialize();
        newLeaf.NextLeaf = oldLeaf.NextLeaf;
        oldLeaf.NextLeaf = newPageNumber;
        newLeaf.Parent = oldLeaf.Parent;

        int insertCell = (int)cursor.CellNumber;

        LeafNode destination;
        int cellInDestination;
        for (int i = Constants.LEAF_NODE_MAX_CELLS; i >= 0; i--)
        {

            if (i >= Constants.LEAF_NODE_LEFT_SPLIT_COUNT)
                destination = newLeaf;
            else destination = oldLeaf;

            cellInDestination = i % Constants.LEAF_NODE_LEFT_SPLIT_COUNT;

            if (i == insertCell)
            {
                destination.SetKeyForCell(cellInDestination, row.Id);
                destination.StoreRow(row, cellInDestination);
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

        oldLeaf.CellCount = Constants.LEAF_NODE_LEFT_SPLIT_COUNT;
        newLeaf.CellCount = Constants.LEAF_NODE_RIGHT_SPLIT_COUNT;

        if (oldLeaf.IsRoot)
            CreateNewRoot(newPageNumber);
        else
        {
            var parentPageNumber = oldLeaf.Parent;
            var newMax = oldLeaf.MaxKey;
            var parent = new InternalNode(Pager.RetrievePage(parentPageNumber));

            parent.UpdateKey(oldMax, newMax);
            InternalNodeInsert(parentPageNumber, newPageNumber);
        }
    }

    public void CreateNewRoot(int rightChildPageNumber)
    {
        var rootPage = Pager.RetrievePage(RootPageNumber);

        var rightChildPage = Pager.RetrievePage(rightChildPageNumber);
        var rootIsInternal = Node.GetNodeType(rootPage) == NodeType.Internal;

        if (rootIsInternal)
            new InternalNode(rightChildPage).Initialize();

        var leftChildPageNumber = Pager.GetUnusedPageNumber();
        var leftChildPage = Pager.RetrievePage(leftChildPageNumber);

        leftChildPage.MarkDirty();
        Array.Copy(rootPage.Data, leftChildPage.Data, Constants.PAGE_SIZE);

        if (rootIsInternal)
        {
            var leftNode = new InternalNode(leftChildPage);

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

        var root = new InternalNode(rootPage);
        root.Initialize();
        root.IsRoot = true;
        root.NumKeys = 1;
        root.SetChild(0, leftChildPageNumber);
        root.SetKeyForCell(0, GetNodeMaxKey(leftChildPage));
        root.RightChild = rightChildPageNumber;
    }

    public void InternalNodeInsert(int parentPageNumber, int childPageNumber)
    {
        var parent = new InternalNode(Pager.RetrievePage(parentPageNumber));
        var child = Pager.RetrievePage(childPageNumber);
        var childMaxKey = GetNodeMaxKey(child);
        var index = parent.FindChildNumber(childMaxKey);
        var originalNumKeys = parent.NumKeys;

        if (originalNumKeys >= Constants.INTERNAL_NODE_MAX_CELLS)
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

        if (childMaxKey > rightChildMaxKey)
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

    public void InternalNodeSplitAndInsert(int parentPageNumber, int childPageNumber)
    {
        var oldPageNumber = parentPageNumber;
        var oldPage = Pager.RetrievePage(oldPageNumber);
        var oldNode = new InternalNode(oldPage);
        var oldMax = GetNodeMaxKey(oldPage);

        var childPage = Pager.RetrievePage(childPageNumber);
        var childMax = GetNodeMaxKey(childPage);

        var newPageNumber = Pager.GetUnusedPageNumber();

        var splittingRoot = oldNode.IsRoot;

        InternalNode parent;

        if (splittingRoot)
        {
            CreateNewRoot(newPageNumber);
            parent = new InternalNode(Pager.RetrievePage(RootPageNumber));
            oldPageNumber = parent.GetChild(0);
            oldNode = new InternalNode(Pager.RetrievePage(oldPageNumber));
        }
        else
        {
            parent = new InternalNode(Pager.RetrievePage(oldNode.Parent));
            new InternalNode(Pager.RetrievePage(newPageNumber)).Initialize();
        }

        var currentPageNumber = oldNode.RightChild;
        InternalNodeInsert(newPageNumber, currentPageNumber);
        Node.SetParent(Pager.RetrievePage(currentPageNumber), newPageNumber);
        oldNode.RightChild = Constants.INVALID_PAGE_NUMBER;

        for (int i = Constants.INTERNAL_NODE_MAX_CELLS - 1; i > Constants.INTERNAL_NODE_MAX_CELLS / 2; i--)
        {
            currentPageNumber = oldNode.GetChild(i);
            InternalNodeInsert(newPageNumber, currentPageNumber);
            Node.SetParent(Pager.RetrievePage(currentPageNumber), newPageNumber);
            oldNode.NumKeys--;
        }

        oldNode.RightChild = oldNode.GetChild((int)oldNode.NumKeys - 1);
        oldNode.NumKeys--;

        var maxAfterSplit = GetNodeMaxKey(Pager.RetrievePage(oldPageNumber));
        var destinationPageNumber = childMax < maxAfterSplit ? oldPageNumber : newPageNumber;

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

    public uint GetNodeMaxKey(Page page)
    {
        var nodeType = Node.GetNodeType(page);
        if (nodeType is NodeType.Leaf)
            return new LeafNode(page).MaxKey;

        var rightChildPageNumber = new InternalNode(page).RightChild;
        return GetNodeMaxKey(Pager.RetrievePage(rightChildPageNumber));
    }

    public void PrintTree(int pageNumber, int indentationLevel)
    {
        var page = Pager.RetrievePage(pageNumber);

        var nodeType = Node.GetNodeType(page);

        switch (nodeType)
        {
            case NodeType.Internal:
                var node = new InternalNode(page);
                Indent(indentationLevel);
                Console.WriteLine($"- internal (size {node.NumKeys})");
                if (node.NumKeys > 0)
                {
                    for (int i = 0; i < node.NumKeys; i++)
                    {
                        PrintTree(node.GetChild(i), indentationLevel + 1);
                        Indent(indentationLevel + 1);
                        Console.WriteLine($"- key {node.GetKey(i)}");
                    }
                    PrintTree(node.RightChild, indentationLevel + 1);
                }
                break;
            case NodeType.Leaf:
                var leaf = new LeafNode(page);
                Indent(indentationLevel);
                Console.WriteLine($"- leaf (size {leaf.CellCount})");
                for (int i = 0; i < leaf.CellCount; i++)
                {
                    Indent(indentationLevel + 1);
                    Console.WriteLine($"- {leaf.GetKey(i)}");
                }
                break;
            default:
                Console.WriteLine("Unexpected node type.");
                Environment.Exit(1);
                break;
        }
    }

    private static void Indent(int level)
    {
        for (int i = 0; i < level; i++)
            Console.Write("  ");
    }
}

