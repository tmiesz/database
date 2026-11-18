internal class Pager
{
    public string FileName { get; }
    public FileStream FileStream { get; }
    private byte[][] Pages { get; } = new byte[Constants.MAX_PAGES][];
    public long Length { get; }
    public int PageCount { get; private set; }

    public Pager(string fileName)
    {
        FileName = fileName;
        FileStream = new FileStream(
                FileName,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite
                );
        Length = FileStream.Length;
        if (Length % Constants.PAGE_SIZE != 0)
        {
            Console.WriteLine("Db file is not a whole number of pages. Corrupt file.");
            Environment.Exit(1);
        }
        PageCount = (int)(Length / Constants.PAGE_SIZE);
    }

    public byte[] RetrievePage(int pageNumber)
    {
        if (pageNumber >= Constants.MAX_PAGES)
        {
            Console.WriteLine($"Tried to fetch page number out of bounds. {pageNumber} > {Constants.MAX_PAGES - 1}");
            Environment.Exit(1);
        }

        byte[] buffer;

        if (Pages[pageNumber] is null)
        {
            buffer = new byte[Constants.PAGE_SIZE];

            var offset = pageNumber * Constants.PAGE_SIZE;
            FileStream.Seek(offset, SeekOrigin.Begin);

            _ = FileStream.Read(buffer, 0, Constants.PAGE_SIZE);

            Pages[pageNumber] = buffer;

            if (pageNumber >= PageCount)
                PageCount = pageNumber + 1;

            return buffer;
        }

        return Pages[pageNumber];
    }

    public bool IsPageLoaded(int pageNumber)
    {
        if (Pages[pageNumber] != null)
            return true;

        else return false;
    }

    public void Flush(int pageNumber)
    {
        if (Pages[pageNumber] is null)
        {
            Console.WriteLine($"Page {pageNumber} is not loaded");
            Environment.Exit(1);
        }
        var offset = pageNumber * Constants.PAGE_SIZE;
        FileStream.Seek(offset, SeekOrigin.Begin);

        FileStream.Write(Pages[pageNumber], 0, Constants.PAGE_SIZE);
        FileStream.Flush();
    }

    public int GetUnusedPageNumber()
    {
        return PageCount;
    }
}

