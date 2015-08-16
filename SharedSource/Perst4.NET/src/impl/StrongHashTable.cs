namespace Perst.Impl
{
    using System;
    using Perst;
	
    public class StrongHashTable : OidHashTable
    {
        internal Entry[] table;
        internal const float loadFactor = 0.75f;
        internal int count;
        internal int threshold;
        internal bool disableRehash;
        internal StorageImpl db;

        const int MODIFIED_BUFFER_SIZE = 1024;
        object[] modified;
        long nModified;

        public StrongHashTable( StorageImpl db, int initialCapacity)
        {
            this.db = db;
            threshold = (int) (initialCapacity * loadFactor);
            if (initialCapacity != 0)
            {
                table = new Entry[initialCapacity];
            }
            modified = new object[MODIFIED_BUFFER_SIZE];
        }
		
        public bool remove(int oid)
        {
            lock(this)
            {
                Entry[] tab = table;
                int index = (oid & 0x7FFFFFFF) % tab.Length;
                for (Entry e = tab[index], prev = null; e != null; prev = e, e = e.next)
                {
                    if (e.oid == oid)
                    {
                        e.oref = null;
                        count -= 1;
                        if (prev != null)
                        {
                            prev.next = e.next;
                        }
                        else
                        {
                            tab[index] = e.next;
                        }
                        return true;
                    }
                }
                return false;
            }
        }
		
        public void  put(int oid, object obj)
        {
            lock(this)
            {
                Entry[] tab = table;
                int index = (oid & 0x7FFFFFFF) % tab.Length;
                for (Entry e = tab[index]; e != null; e = e.next)
                {
                    if (e.oid == oid)
                    {
                        e.oref = obj;
                        return ;
                    }
                }
                if (count >= threshold && !disableRehash)
                {
                    // Rehash the table if the threshold is exceeded
                    rehash();
                    tab = table;
                    index = (oid & 0x7FFFFFFF) % tab.Length;
                }
				
                // Creates the new entry.
                tab[index] = new Entry(oid, obj, tab[index]);
                count++;
            }
        }
		
        public object get(int oid)
        {
            lock(this)
            {
                Entry[] tab = table;
                int index = (oid & 0x7FFFFFFF) % tab.Length;
                for (Entry e = tab[index]; e != null; e = e.next)
                {
                    if (e.oid == oid)
                    {
                        return e.oref;
                    }
                }
                return null;
            }
        }
		
        public void flush() 
        {
            lock(this) 
            {
                long n;
                do
                { 
                    n = nModified;
                    if (n < MODIFIED_BUFFER_SIZE) 
                    { 
                        object[] mod = modified;
                        for (int i = (int)n; --i >= 0;) 
                        { 
                            object obj = mod[i];
                            if (db.IsModified(obj)) 
                            { 
                                db.Store(obj);
                            }
                        }
                    }
                    else 
                    { 
                        Entry[] tab = table;
                        disableRehash = true;
                        for (int i = 0; i < tab.Length; i++) 
                        { 
                            for (Entry e = tab[i]; e != null; e = e.next) 
                            { 
                                if (db.IsModified(e.oref)) 
                                { 
                                    db.Store(e.oref);
                                }
                            }
                        }
                        disableRehash = false;
                        if (count >= threshold) 
                        {
                            // Rehash the table if the threshold is exceeded
                            rehash();
                        }
                    }
                } while (n != nModified);
                nModified = 0;
            }
        }

        public void reload() 
        {
            lock (this) 
            { 
                disableRehash = true;
                Entry[] tab = table;
                for (int i = 0; i < tab.Length; i++) 
                { 
                    for (Entry e = tab[i]; e != null; e = e.next) 
                    { 
                        db.Invalidate(e.oref);
                        try { 
                            db.Load(e.oref);
                        } catch (Exception x) { 
                            // ignore errors caused by attempt to load object which was created in rollbacked transaction
                        }
                    }
                }
                disableRehash = false;
                if (count >= threshold) 
                {
                    // Rehash the table if the threshold is exceeded
                    rehash();
                }
            }
        }

        public void clear() 
        {
            lock(this) 
            {
                Entry[] tab = table;
                for (int i = 0; i < tab.Length; i++)
                { 
                    tab[i] = null;
                }
                count = 0;
                nModified = 0;
            }
        }

        public void invalidate() 
        {
            lock(this) 
            {
                for (int i = 0; i < table.Length; i++) 
                { 
                    for (Entry e = table[i]; e != null; e = e.next) 
                    { 
                        if (db.IsModified(e.oref)) 
                        { 
                            db.Invalidate(e.oref);
                        }
                    }
                }
            }
        }
    

        internal void  rehash()
        {
            int oldCapacity = table.Length;
            Entry[] oldMap = table;
            int i;

            int newCapacity = oldCapacity * 2 + 1;
            Entry[] newMap = new Entry[newCapacity];
			
            threshold = (int) (newCapacity * loadFactor);
            table = newMap;
			
            for (i = oldCapacity; --i >= 0; )
            {
                for (Entry old = oldMap[i]; old != null; )
                {
                    Entry e = old;
                    old = old.next;
					
                    int index = (e.oid & 0x7FFFFFFF) % newCapacity;
                    e.next = newMap[index];
                    newMap[index] = e;
                }
            }
        }
		
         
        public int size()
        {
            return count;
        }

        	
        public void setDirty(object obj) 
        {
            if (nModified < MODIFIED_BUFFER_SIZE) 
            { 
                modified[(int)nModified] = obj;
            }
            nModified += 1;
        } 

        public void clearDirty(object obj) 
        {
        }

        internal class Entry
        {
            internal Entry next;
            internal object oref;
            internal int oid;
		
            internal Entry(int oid, object oref, Entry chain)
            {
                next = chain;
                this.oid = oid;
                this.oref = oref;
            }
        }
    }
}