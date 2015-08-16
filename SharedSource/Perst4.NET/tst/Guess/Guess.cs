using System;
using Perst;

public class Guess:Persistent
{
    public Guess  yes;
    public Guess  no;
    public string question;
	
    public Guess(Guess no, string question, Guess yes)
    {
        this.yes = yes;
        this.question = question;
        this.no = no;
    }
	
    internal Guess() {} 

    internal static string input(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string line = Console.ReadLine().Trim();
            if (line.Length != 0) 
            { 
                return line;
            }
        }
    }
	
    internal static bool askQuestion(string question)
    {
        string answer = input(question);
        return answer.ToUpper().Equals("y".ToUpper()) || answer.ToUpper().Equals("yes".ToUpper());
    }
	
    internal static Guess whoIsIt(Guess parent)
    {
        string animal = input("What is it ? ");
        string difference = input("What is a difference from other ? ");
        return new Guess(parent, difference, new Guess(null, animal, null));
    }
	
    internal Guess dialog()
    {
        if (askQuestion("May be, " + question + " (y/n) ? "))
        {
            if (yes == null)
            {
                Console.WriteLine("It was very simple question for me...");
            }
            else
            {
                Guess clarify = yes.dialog();
                if (clarify != null)
                {
                    yes = clarify;
                    Store();
                }
            }
        }
        else
        {
            if (no == null)
            {
                if (yes == null)
                {
                    return whoIsIt(this);
                }
                else
                {
                    no = whoIsIt(null);
                    Store();
                }
            }
            else
            {
                Guess clarify = no.dialog();
                if (clarify != null)
                {
                    no = clarify;
                    Store();
                }
            }
        }
        return null;
    }
	
    static public void  Main(string[] args)
    {
        Storage db = StorageFactory.Instance.CreateStorage();
		
        bool multiclient = args.Length > 0 && args[0].StartsWith("multi");
        if (multiclient) 
        { 
            db.SetProperty("perst.multiclient.support", true);
        }

        db.Open("guess.dbs", 4*1024*1024, "GUESS");
		
        while (askQuestion("Think of an animal. Ready (y/n) ? "))
        {
            if (multiclient) 
            { 
                db.BeginThreadTransaction(TransactionMode.ReadWrite);
            }
            Guess root = (Guess) db.Root;
            if (root == null)
            {
                root = whoIsIt(null);
                db.Root = root;
            }
            else
            {
                root.dialog();
            }
            if (multiclient) 
            { 
                db.EndThreadTransaction();
            } 
            else 
            { 
                db.Commit();
            }
        }
		
        Console.WriteLine("End of the game");
        db.Close();
    }
}