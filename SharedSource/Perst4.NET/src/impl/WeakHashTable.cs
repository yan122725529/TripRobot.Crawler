namespace Perst.Impl
{
    using System;
    using Perst;
	
    public class WeakHashTable : OidHashTable
    {
        internal Entry[] table;
        internal const float loadFactor = 0.75f;
        internal int count;
        internal int threshold;
        internal long nModified;
        internal bool disableRehash;
	internal StorageImpl db;

        public WeakHashTable(StorageImpl db, int initialCapacity)
        {
            threshold = (int) (initialCapacity * loadFactor);
            table = new Entry[initialCapacity];
            this.db = db;
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
            while (true) 
            { 
                lock(this)
                {
                    Entry[] tab = table;
                    int index = (oid & 0x7FFFFFFF) % tab.Length;
                    for (Entry e = tab[index], prev = null; e != null; prev = e, e = e.next)
                    {
                        if (e.oid == oid)
                        {
                            object obj = e.oref.Target;
                            if (obj == null) 
                            {
                                if (e.dirty > 0) 
                                {
                                    goto waitFinalization;
                                }
                            } 
                            else if (db.IsDeleted(obj)) 
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
                                return null;
                            }
                            return obj;
                        }
                    }
                    return null;
                }
            waitFinalization:
                GC.WaitForPendingFinalizers();
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
                    if ((obj == null || db.IsDeleted(obj)) && e.dirty == 0)
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
                        object obj = e.oref.Target;
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
            while (true) 
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
                                object obj = e.oref.Target;
                                if (obj != null) 
                                { 
                                    if (db.IsModified(obj)) 
                                    { 
                                        db.Store(obj);
                                    }
                                } 
                                else if (e.dirty != 0) 
                                { 
                                    goto waitFinalization;
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
                    return;
                }
            waitFinalization:
                GC.WaitForPendingFinalizers();
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
            while (true) 
            {
                lock (this) 
                { 
                    for (int i = 0; i < table.Length; i++) 
                    { 
                        for (Entry e = table[i]; e != null; e = e.next) 
                        { 
                            object obj = e.oref.Target;
                            if (obj != null) 
                            { 
                                if (db.IsModified(obj)) 
                                { 
                                    e.dirty = 0;
                                    db.Invalidate(obj);
                                }
                            } 
                            else if (e.dirty != 0) 
                            { 
                                goto waitFinalization;
                            }
                        }
                    }
                    return;
                }
            waitFinalization:
                GC.WaitForPendingFinalizers();
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
                        e.dirty += 1;
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
                        if (e.oref.IsAlive) 
                        { 
                            if (e.dirty > 0) 
                            { 
                                e.dirty -= 1;
                            }
                        } 
                        else 
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
                        }
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
            internal int dirty;
		
            internal void clear() 
            { 
                oref = null;
                dirty = 0;
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


