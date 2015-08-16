using System;
using System.Diagnostics;

using Perst;
using Perst.FullText;
using Perst.Assoc;

public class Library
{
    int nAuthors;
    int nCoauthors = 2;
    int nBooksPerAutor = 10;

    const int MAX_FULL_TEXT_SEARCH_RESULTS = 1000;
    const int MAX_FULL_TEXT_SEARCH_TIME = 10000; // 10 seconds

    static String GenerateWord(int x) 
    { 
        char[] chars = new char[8];
        for (int i = 0; i < 8; i++) { 
            chars[i] = (char)('A' + (x & 0xF));
            x >>= 4;
        }
        return new String(chars);
    }

    static String GenerateTitle(int x)
    {
        return GenerateWord(x) + " " + GenerateWord(~x);
    }

    static String GenerateName(int x)
    {
        return "Mr" + GenerateWord(x) + " Mr" + GenerateWord(~x);
    }
            
    static String GenerateISBN(int x) 
    {
        return x.ToString();
    }
        
    
    void PopulateDatabase()
    {
        DateTime start = DateTime.Now;
        ReadWriteTransaction t = db.StartReadWriteTransaction();
        
        if (t.Verbs.Count == 0) { 
            int nBooks = nAuthors * nBooksPerAutor / nCoauthors;
            Item[] author = new Item[nAuthors];
            for (int i = 0; i < nAuthors; i++) { 
                author[i] = t.CreateItem();
                t.Link(author[i], "name",  GenerateName(i));
                t.IncludeInFullTextIndex(author[i]);
            }
            for (int i = 0, j = 0; i < nBooks; i++) { 
                Item book = t.CreateItem();
                t.Link(book, "title", GenerateTitle(i));
                t.Link(book, "ISBN", GenerateISBN(i));
                t.Link(book, "publish-date", DateTime.Now.ToString());
                t.IncludeInFullTextIndex(book, new String[]{"title", "ISBN"});
                for (int k = 0; k < nCoauthors; k++) { 
                    t.Link(book, "author", author[j++ % nAuthors]);
                }
            }
            Console.WriteLine("Elapsed time for populating database: " + (DateTime.Now - start));
        }
        t.Commit();
    }
    
    void SearchDatabase() 
    {
        ReadOnlyTransaction t = db.StartReadWriteTransaction();

        DateTime start = DateTime.Now;
        int nBooks = nAuthors * nBooksPerAutor / nCoauthors;
        for (int i = 0, j = 0; i < nBooks; i++) { 
            // find authors of the book
            Item[] authors = Enumerable.ToArray(t.Find(Predicate.Value("-author").In(Predicate.Value("title") == GenerateTitle(i))));
            Debug.Assert(authors.Length == nCoauthors);
            for (int k = 0; k < nCoauthors; k++) { 
                Debug.Assert(authors[k].GetString("name") == GenerateName(j++ % nAuthors));
            }
        }
        for (int i = 0; i < nAuthors; i++) { 
            // find book written by this author
            Item[] books = Enumerable.ToArray(t.Find(Predicate.Value("author").In(Predicate.Value("name") == GenerateName(i))));
            Debug.Assert(books.Length == nBooksPerAutor);
        }
        Console.WriteLine("Elapsed time for searching database " + (DateTime.Now - start));

        start = DateTime.Now;
        for (int i = 0, mask = 0; i < nBooks; i++, mask = ~mask) { 
            // find book using full text search part of book title and ISDN
            FullTextSearchResult result = t.FullTextSearch(GenerateWord(i ^ mask) + " " + GenerateISBN(i), MAX_FULL_TEXT_SEARCH_RESULTS, MAX_FULL_TEXT_SEARCH_TIME);
            Debug.Assert(result.Hits.Length == 1);
        }
        for (int i = 0, mask = 0; i < nAuthors; i++, mask = ~mask) { 
            // find authors using full text search of author's name
            FullTextSearchResult result = t.FullTextSearch(GenerateName(i ^ mask), MAX_FULL_TEXT_SEARCH_RESULTS, MAX_FULL_TEXT_SEARCH_TIME);
            Debug.Assert(result.Hits.Length == 1);
        }
        Console.WriteLine("Elapsed time for full text search " + (DateTime.Now - start));

        t.Commit();
    }
            
    void Shutdown()
    {
        storage.Close();
    }

    Library(int authors)
    {
        nAuthors = authors;
        storage = StorageFactory.Instance.CreateStorage();
        storage.Open("library.dbs");
        db = new AssocDB(storage);
    }
     
    public static void Main(String[] args) 
    {
        int nAuthors = args.Length > 0 ? int.Parse(args[0]) : 10000;
        Library library = new Library(nAuthors);
        library.PopulateDatabase();
        library.SearchDatabase();
        library.Shutdown();
    }
   
    AssocDB db;
    Storage storage;
}