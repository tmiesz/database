internal class Pager
{
    public string FileName { get; }
    private string JournalFileName => $"{FileName}.journal";
    public FileStream FileStream { get; }
    private Page?[] Pages { get; } = new Page?[Constants.MAX_PAGES];
    public int PageCount { get; private set; }
    public bool IsTransactionOpen { get; private set; }

    public Pager(string fileName)
    {
        FileName = fileName;
        FileStream = new FileStream(
                FileName,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite
                );
        var length = FileStream.Length;
        if (length % Constants.PAGE_SIZE != 0)
        {
            Console.WriteLine("Db file is not a whole number of pages. Corrupt file.");
            Environment.Exit(1);
        }
        PageCount = (int)(length / Constants.PAGE_SIZE);
    }


    public Page RetrievePage(int pageNumber)
    {
        if (pageNumber >= Constants.MAX_PAGES)
        {
            Console.WriteLine($"Tried to fetch page number out of bounds. {pageNumber} > {Constants.MAX_PAGES - 1}");
            Environment.Exit(1);
        }

        byte[] buffer;
        var page = Pages[pageNumber];

        if (page is not null)
            return page;

        buffer = new byte[Constants.PAGE_SIZE];

        var offset = pageNumber * Constants.PAGE_SIZE;
        FileStream.Seek(offset, SeekOrigin.Begin);

        _ = FileStream.Read(buffer, 0, Constants.PAGE_SIZE);

        page = new Page(pageNumber, buffer);
        Pages[pageNumber] = page;

        if (pageNumber >= PageCount)
            PageCount = pageNumber + 1;

        return page;
    }

    public void Flush(int pageNumber)
    {
        var page = Pages[pageNumber];

        if (page is null)
        {
            Console.WriteLine($"Page {pageNumber} is not loaded");
            Environment.Exit(1);
        }

        var offset = pageNumber * Constants.PAGE_SIZE;
        FileStream.Seek(offset, SeekOrigin.Begin);

        FileStream.Write(page.Data, 0, Constants.PAGE_SIZE);
    }

    public int GetUnusedPageNumber()
    {
        return PageCount;
    }

    public List<int> GetDirtyPages()
    {
        var pages = new List<int>();

        foreach (var page in Pages)
        {
            if (page is not null && page.IsDirty)
                pages.Add(page.PageNumber);
        }

        return pages;
    }

    public TransactionOutcome Begin()
    {
        if (IsTransactionOpen)
            return TransactionOutcome.AlreadyOpen;

        IsTransactionOpen = true;

        return TransactionOutcome.Success;
    }

    public TransactionOutcome Commit()
    {
        if (!IsTransactionOpen)
            return TransactionOutcome.NotOpen;

        WritePages();

        IsTransactionOpen = false;

        return TransactionOutcome.Success;
    }

    public void CompleteStatement()
    {
        if (IsTransactionOpen)
            return;

        WritePages();
    }

    public TransactionOutcome Rollback()
    {
        if (!IsTransactionOpen)
            return TransactionOutcome.NotOpen;

        var pages = GetDirtyPages();

        foreach (var page in pages)
            Pages[page] = null;

        PageCount = (int)(FileStream.Length / Constants.PAGE_SIZE);

        IsTransactionOpen = false;

        return TransactionOutcome.Success;
    }

    public void Close()
    {
        FileStream.Dispose();
    }

    private void WritePages()
    {
        var pages = GetDirtyPages();
        if (pages.Count == 0)
            return;

        var originalPageCount = (int)FileStream.Length / Constants.PAGE_SIZE;
        WriteJournal(pages, originalPageCount);
        CrashPoint.At("after-journal-records");

        for (int i = 0; i < pages.Count; i++)
        {
            Flush(pages[i]);

            if (i == 0)
                CrashPoint.At("after-first-page-write");
        }

        FileStream.Flush(true);

        File.Delete(JournalFileName);

        foreach (var page in pages)
            Pages[page]!.ClearDirty();
    }

    private void WriteJournal(List<int> pages, int originalPageCount)
    {
        using var journal = new FileStream(JournalFileName, FileMode.Create, FileAccess.Write);
        journal.Position = Constants.JOURNAL_HEADER_SIZE;

        var original = new byte[Constants.PAGE_SIZE];

        foreach (var pageNumber in pages)
        {
            if (pageNumber >= originalPageCount)
                continue;

            FileStream.Seek(pageNumber * Constants.PAGE_SIZE, SeekOrigin.Begin);
            FileStream.ReadExactly(original, 0, Constants.PAGE_SIZE);

            var bytes = BitConverter.GetBytes(pageNumber);
            journal.Write(bytes, 0, bytes.Length);
            journal.Write(original, 0, Constants.PAGE_SIZE);
        }

        journal.Flush(true);
    }
}

