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
        Recover();
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
        CrashPoint.At("after-journal-header");

        for (int i = 0; i < pages.Count; i++)
        {
            Flush(pages[i]);

            if (i == 0)
                CrashPoint.At("after-first-page-write");
        }

        FileStream.Flush(true);

        CrashPoint.At("after-database-written");
        File.Delete(JournalFileName);

        foreach (var page in pages)
            Pages[page]!.ClearDirty();
    }

    private void WriteJournal(List<int> pages, int originalPageCount)
    {
        using var journal = new FileStream(JournalFileName, FileMode.Create, FileAccess.Write);
        CrashPoint.At("after-journal-created");
        journal.Position = Constants.JOURNAL_HEADER_SIZE;

        var original = new byte[Constants.PAGE_SIZE];

        var recordCount = 0;

        foreach (var pageNumber in pages)
        {
            if (pageNumber >= originalPageCount)
                continue;

            FileStream.Seek(pageNumber * Constants.PAGE_SIZE, SeekOrigin.Begin);
            FileStream.ReadExactly(original, 0, Constants.PAGE_SIZE);

            var bytes = BitConverter.GetBytes(pageNumber);
            journal.Write(bytes, 0, bytes.Length);
            journal.Write(original, 0, Constants.PAGE_SIZE);

            recordCount++;
        }

        journal.Flush(true);

        CrashPoint.At("after-journal-records");

        var header = new byte[Constants.JOURNAL_HEADER_SIZE];

        var magicBytes = BitConverter.GetBytes(Constants.JOURNAL_MAGIC);
        Array.Copy(magicBytes, 0, header, Constants.JOURNAL_MAGIC_OFFSET, magicBytes.Length);
        var originalPageCountBytes = BitConverter.GetBytes(originalPageCount);
        Array.Copy(originalPageCountBytes, 0, header, Constants.JOURNAL_ORIGINAL_PAGE_COUNT_OFFSET, originalPageCountBytes.Length);
        var recordCountBytes = BitConverter.GetBytes(recordCount);
        Array.Copy(recordCountBytes, 0, header, Constants.JOURNAL_RECORD_COUNT_OFFSET, recordCountBytes.Length);

        journal.Position = 0;
        journal.Write(header, 0, header.Length);
        journal.Flush(true);
    }

    private void Recover()
    {
        if (!File.Exists(JournalFileName))
            return;

        using (var journal = new FileStream(JournalFileName, FileMode.Open, FileAccess.Read))
        {
            var isValid = false;
            var header = new byte[Constants.JOURNAL_HEADER_SIZE];

            if (journal.Length >= Constants.JOURNAL_HEADER_SIZE)
            {
                journal.ReadExactly(header, 0, header.Length);
                isValid = BitConverter.ToUInt32(header, Constants.JOURNAL_MAGIC_OFFSET) == Constants.JOURNAL_MAGIC;
            }

            if (isValid)
            {
                var originalPageCount = BitConverter.ToInt32(header, Constants.JOURNAL_ORIGINAL_PAGE_COUNT_OFFSET);
                var recordCount = BitConverter.ToInt32(header, Constants.JOURNAL_RECORD_COUNT_OFFSET);

                var expectedLength = Constants.JOURNAL_HEADER_SIZE + (long)recordCount * Constants.JOURNAL_RECORD_SIZE;
                if (recordCount < 0
                    || originalPageCount < 0
                    || originalPageCount > Constants.MAX_PAGES
                    || journal.Length != expectedLength)
                {
                    Console.WriteLine("Journal is damaged: its size does not match its header. Database not changed.");
                    Environment.Exit(1);
                }

                var record = new byte[Constants.JOURNAL_RECORD_SIZE];

                for (int i = 0; i < recordCount; i++)
                {
                    journal.ReadExactly(record, 0, record.Length);
                    var pageNumber = BitConverter.ToInt32(record, Constants.JOURNAL_PAGE_NUMBER_OFFSET);

                    FileStream.Seek(pageNumber * Constants.PAGE_SIZE, SeekOrigin.Begin);
                    FileStream.Write(record, Constants.JOURNAL_PAGE_DATA_OFFSET, Constants.PAGE_SIZE);
                }

                FileStream.SetLength(originalPageCount * Constants.PAGE_SIZE);
                FileStream.Flush(true);
                CrashPoint.At("after-restore");
            }
        }

        File.Delete(JournalFileName);
    }
}

