static class Constants
{
    public const int COMMON_NODE_HEADER_SIZE = NodeTypeSize + IsRootFlagSize + ParentPointerSize;
    public const int NODE_TYPE_OFFSET = 0;
    public const int IS_ROOT_OFFSET = NODE_TYPE_OFFSET + NodeTypeSize;
    public const int PARENT_POINTER_OFFSET = IS_ROOT_OFFSET + IsRootFlagSize;
    public const int INVALID_PAGE_NUMBER = -1;

    public const int LEAF_NODE_NEXT_LEAF_SIZE = LeafNodeNextLeafSize;
    public const int LEAF_NODE_NEXT_LEAF_OFFSET = LEAF_NODE_CELL_COUNT_OFFSET + CellCountSize;
    public const int INTERNAL_NODE_NUM_KEYS_SIZE = InternalNodeNumKeysSize;
    public const int INTERNAL_NODE_NUM_KEYS_OFFSET = COMMON_NODE_HEADER_SIZE;
    public const int INTERNAL_NODE_RIGHT_CHILD_SIZE = InternalNodeChildPointerSize;
    public const int INTERNAL_NODE_RIGHT_CHILD_OFFSET = COMMON_NODE_HEADER_SIZE + INTERNAL_NODE_NUM_KEYS_SIZE;
    public const int INTERNAL_NODE_HEADER_SIZE = COMMON_NODE_HEADER_SIZE + INTERNAL_NODE_NUM_KEYS_SIZE + INTERNAL_NODE_RIGHT_CHILD_SIZE;
    public const int INTERNAL_NODE_CELL_CHILD_SIZE = InternalNodeChildPointerSize;
    public const int INTERNAL_NODE_KEY_OFFSET = INTERNAL_NODE_CELL_CHILD_SIZE;
    public const int INTERNAL_NODE_CELL_SIZE = INTERNAL_NODE_CELL_CHILD_SIZE + KeySize;
    public const int INTERNAL_NODE_MAX_CELLS = 3;
    public const int INTERNAL_NODE_CAPACITY = (PAGE_SIZE - INTERNAL_NODE_HEADER_SIZE) / INTERNAL_NODE_CELL_SIZE;

    public const int LEAF_NODE_CELL_COUNT_OFFSET = COMMON_NODE_HEADER_SIZE;
    public const int LEAF_NODE_VALUE_OFFSET = KeySize;
    public const int LEAF_NODE_HEADER_SIZE = COMMON_NODE_HEADER_SIZE + CellCountSize + LEAF_NODE_NEXT_LEAF_SIZE;
    public const int LEAF_NODE_CELL_SIZE = KeySize + Row.ROW_SIZE;
    public const int LEAF_NODE_SPACE_FOR_CELLS = PAGE_SIZE - LEAF_NODE_HEADER_SIZE;
    public const int LEAF_NODE_MAX_CELLS = LEAF_NODE_SPACE_FOR_CELLS / LEAF_NODE_CELL_SIZE;
    public const int LEAF_NODE_RIGHT_SPLIT_COUNT = (LEAF_NODE_MAX_CELLS + 1) / 2;
    public const int LEAF_NODE_LEFT_SPLIT_COUNT = LEAF_NODE_MAX_CELLS + 1 - LEAF_NODE_RIGHT_SPLIT_COUNT;

    public const int MAX_PAGES = 100;
    public const int PAGE_SIZE = 4096;

    public const int JOURNAL_MAGIC_OFFSET = 0;
    public const int JOURNAL_ORIGINAL_PAGE_COUNT_OFFSET = JOURNAL_MAGIC_OFFSET + JournalMagicSize;
    public const int JOURNAL_RECORD_COUNT_OFFSET = JOURNAL_ORIGINAL_PAGE_COUNT_OFFSET + JournalOriginalPageCountSize;
    public const int JOURNAL_PAGE_NUMBER_OFFSET = 0;
    public const int JOURNAL_PAGE_DATA_OFFSET = JOURNAL_PAGE_NUMBER_OFFSET + JournalPageNumberSize;
    public const int JOURNAL_HEADER_SIZE = JournalMagicSize + JournalOriginalPageCountSize + JournalRecordCountSize;
    public const int JOURNAL_RECORD_SIZE = JournalPageNumberSize + PAGE_SIZE;
    public const uint JOURNAL_MAGIC = 0xf0928480;

    private const int NodeTypeSize = 1;
    private const int IsRootFlagSize = 1;
    private const int ParentPointerSize = 4;
    private const int CellCountSize = 4;
    private const int KeySize = 4;
    private const int InternalNodeNumKeysSize = 4;
    private const int InternalNodeChildPointerSize = 4;
    private const int LeafNodeNextLeafSize = 4;
    private const int JournalMagicSize = 4;
    private const int JournalOriginalPageCountSize = 4;
    private const int JournalRecordCountSize = 4;
    private const int JournalPageNumberSize = 4;

    internal static void Print()
    {
        Console.WriteLine("Constants:");
        Console.WriteLine($"{nameof(Row.ROW_SIZE)}: {Row.ROW_SIZE}");
        Console.WriteLine($"{nameof(COMMON_NODE_HEADER_SIZE)}: {COMMON_NODE_HEADER_SIZE}");
        Console.WriteLine($"{nameof(LEAF_NODE_HEADER_SIZE)}: {LEAF_NODE_HEADER_SIZE}");
        Console.WriteLine($"{nameof(LEAF_NODE_CELL_SIZE)}: {LEAF_NODE_CELL_SIZE}");
        Console.WriteLine($"{nameof(LEAF_NODE_SPACE_FOR_CELLS)}: {LEAF_NODE_SPACE_FOR_CELLS}");
        Console.WriteLine($"{nameof(LEAF_NODE_MAX_CELLS)}: {LEAF_NODE_MAX_CELLS}");
        Console.WriteLine($"{nameof(LEAF_NODE_RIGHT_SPLIT_COUNT)}: {LEAF_NODE_RIGHT_SPLIT_COUNT}");
        Console.WriteLine($"{nameof(LEAF_NODE_LEFT_SPLIT_COUNT)}: {LEAF_NODE_LEFT_SPLIT_COUNT}");
        Console.WriteLine($"{nameof(INTERNAL_NODE_HEADER_SIZE)}: {INTERNAL_NODE_HEADER_SIZE}");
        Console.WriteLine($"{nameof(INTERNAL_NODE_CELL_SIZE)}: {INTERNAL_NODE_CELL_SIZE}");
        Console.WriteLine($"{nameof(INTERNAL_NODE_MAX_CELLS)}: {INTERNAL_NODE_MAX_CELLS}");
        Console.WriteLine($"{nameof(INTERNAL_NODE_CAPACITY)}: {INTERNAL_NODE_CAPACITY}");
    }
}
