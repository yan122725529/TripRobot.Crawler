namespace Perst.Impl
{
    using System;
    using Perst;
	
    public class PinWeakHashTable : OidHashTable
    {
        internal Entry[] table;
        internal const float loadFactor = 0.75f;
        internal int count;
        internal int threshold;
        internal long nModified;
        internal bool disableRehash;
        internal StorageImpl db;
		
        public PinWeakHashTable(StorageImpl db, int initialCapacity)
        {
            this.db = db;
            threshold = (int) (initialCapacity * loadFactor);
            table = new Entry[initialCapacity];
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
                        if (prev != null)
                        {
                            prev.next = e.next;
                        }
                        else
                        {
                            tab[index] = e.next;
                        }
                        e.clear();
                        count -= 1;
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
                        e.oref.Target = obj;
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
                tab[index] = new Entry(oid, new WeakReference(obj), tab[index]);
                count++;
            }
        }
		
        public object get(int oid)
        {
            lock(this)
            {
                Entry[] tab = table;
                int index = (oid & 0x7FFFFFFF) % tab.Length;
                for (Entry e = tab[index], prev = null; e != null; prev = e, e = e.next)
                {
                    if (e.oid == oid)
                    {
                        if (e.pin != null) 
                        {
                            return e.pin;
                        } 
                        return e.oref.Target;
                    }
                }
                return null;
            }
        }
		

        internal void  rehash()
        {
            int oldCapacity = table.Length;
            Entry[] oldMap = table;
            int i;
            for (i = oldCapacity; --i >= 0; )
            {
                Entry e, next, prev;
                for (prev = null, e = oldMap[i]; e != null; e = next) 
                { 
                    next = e.next;
                    object obj = e.oref.Target;
                    if ((obj == null || db.IsDeleted(obj)) && e.pin == null)
                    {
                        count -= 1;
                        e.clear();
                        if (prev == null)
                        {
                            oldMap[i] = next;
                        }
                        else
                        {
                            prev.next = next;
                        }
                    }
                    else
                    {
                        prev = e;
                    }
                }
            }
			
            if ((uint)count <= ((uint)threshold >> 1))
            {
                return ;
            }
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
                        object obj = e.pin;
                        if (obj != null) 
                        { 
                            db.Invalidate(obj);
                            try { 
                                db.Load(obj);
                            } catch (Exception x) { 
                                // ignore errors caused by attempt to load object which was created in rollbacked transaction
                            }
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

        public void flush() 
        {
            lock (this) 
            { 
                disableRehash = true;
                long n;
                do  
                { 
                    n = nModified;
                    for (int i = 0; i < table.Length; i++) 
                    { 
                        for (Entry e = table[i]; e != null; e = e.next) 
                        { 
                            object obj = e.pin;
                            if (obj != null) 
                            { 
                                e.pin = null;
                                db.Store(obj);
                            } 
                        }
                    }
                } while (n != nModified);

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
            }
        }

        public void invalidate() 
        {
            lock (this) 
            { 
                for (int i = 0; i < table.Length; i++) 
                { 
                    for (Entry e = table[i]; e != null; e = e.next) 
                    { 
                        object obj = e.pin;
                        if (obj != null) 
                        { 
                            e.pin = null;
                            db.Invalidate(obj);
                        }
                    }
                }
            }
        }

        public void setDirty(object obj) 
        {
            lock (this) 
            { 
                int oid = db.GetOid(obj);
                Entry[] tab = table;
                int index = (oid & 0x7FFFFFFF) % tab.Length;
                nModified += 1;
                for (Entry e = tab[index] ; e != null ; e = e.next) 
                {
                    if (e.oid == oid) 
                    {
                        e.pin = obj;
                        return;
                    }
                }
            }
        }

        public void clearDirty(object obj) 
        {
            lock (this) 
            { 
                int oid = db.GetOid(obj);
                Entry[] tab = table;
                int index = (oid & 0x7FFFFFFF) % tab.Length;
                for (Entry e = tab[index], prev = null; e != null; prev = e, e = e.next)
                {
                    if (e.oid == oid) 
                    {
                        e.pin = null;
                        return;
                    }
                }
            }
        }

        public int size()
        {
            return count;
        }

	
        internal class Entry
        {
            internal Entry next;
            internal WeakReference oref;
            internal int oid;
            internal object pin;
		
            internal void clear() 
            { 
                oref = null;
                pin = null;
                next = null;
            }

            internal Entry(int oid, WeakReference oref, Entry chain)
            {
                next = chain;
                this.oid = oid;
                this.oref = oref;
            }
        }
    }
}


