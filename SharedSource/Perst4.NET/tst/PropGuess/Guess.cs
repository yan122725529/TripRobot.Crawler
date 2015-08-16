using System;
using Perst;


public abstract class Guess : Persistent
{
    public abstract Guess Yes 
    {
        set;
        get;
    }

    public abstract Guess No 
    {
        set;
        get;
    }

    public abstract string Question
    {
        set;
        get;
    }

    internal static Guess Create(Storage storage, Guess no, string question, Guess yes)
    {
        Guess guess = (Guess)storage.CreateClass(typeof(Guess));
        guess.Yes = yes;
        guess.Question = question;
        guess.No = no;
        return guess;
    }
	
    internal static string input(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            String line = Console.ReadLine().Trim();
            if (line.Length != 0) 
            { 
                return line;
            }
        }
    }
	
    internal static bool askQuestion(System.String question)
    {
        string answer = input(question);
        return answer.ToUpper().Equals("Y") || answer.ToUpper().Equals("YES");
    }
	
    internal static Guess whoIsIt(Storage storage, Guess parent)
    {
        System.String animal = input("What is it ? ");
        System.String difference = input("What is a difference from other ? ");
        return Create(storage, parent, difference, Create(storage, null, animal, null));
    }
	
    internal Guess dialog()
    {
        if (askQuestion("May be, " + Question + " (y/n) ? "))
        {
            if (Yes == null)
            {
                Console.WriteLine("It was very simple question for me...");
            }
            else
            {
                Guess clarify = Yes.dialog();
                if (clarify != null)
                {
                    Yes = clarify;
                }
            }
        }
        else
        {
            if (No == null)
            {
                if (Yes == null)
                {
                    return whoIsIt(Storage, this);
                }
                else
                {
                    No = whoIsIt(Storage, null);
                }
            }
            else
            {
                Guess clarify = No.dialog();
                if (clarify != null)
                {
                    No = clarify;
                }
            }
        }
        return null;
    }
	
    static public void Main(string[] args)
    {
        Storage db = StorageFactory.Instance.CreateStorage();
		
        db.Open("guess.dbs", 4*1024*1024, "GUESS");
        Guess root = (Guess) db.Root;
		
        while (askQuestion("Think of an animal. Ready (y/n) ? "))
        {
            if (root == null)
            {
                root = whoIsIt(db, null);
                db.Root = root;
            }
            else
            {
                root.dialog();
            }
            db.Commit();
        }
		
        System.Console.WriteLine("End of the game");
        db.Close();
    }
}