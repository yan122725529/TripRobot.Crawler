namespace Perst.Impl
{
    using System;
    using Perst;
    using System.Runtime.CompilerServices;

	
    internal class ObjectMap
    {
        internal Entry[] table;
        internal const float loadFactor = 0.75f;
        internal int count;
        internal int threshold;
		
        internal ObjectMap(int initialCapacity)
        {
            threshold = (int) (initialCapacity * loadFactor);
            table = new Entry[initialCapacity];
        }

        private static uint GetIdentityHashCode(object obj)
        {
#if COMPACT_NET_FRAMEWORK
             return (uint)obj.GetHashCode();
#else
             return (uint)RuntimeHelpers.GetHashCode(obj);
#endif
        }

		
        internal bool Remove(object obj)
        {
            lock(this)
            {                
                Entry[] tab = table;
                int hashcode = (int)(GetIdentityHashCode(obj) % tab.Length);
                for (Entry e = tab[hashcode], prev = null; e != null; e = e.next)
                {
                    object target = e.wref.Target;
                    if (target == null) 
                    { 
                        if (prev != null)
                        {
                            prev.next = e.next;
                        }
                        else
                        {
                            tab[hashcode] = e.next;
                        }
                        e.clear();
                        count -= 1;
                    } 
                    else if (Object.ReferenceEquals(target, obj))
                    {
                        if (prev != null)
                        {
                            prev.next = e.next;
                        }
                        else
                        {
                            tab[hashcode] = e.next;
                        }
                        e.clear();
                        count -= 1;
                        return true;
                    } 
                    else 
                    {
                        prev = e;
                    }
                }
                return false;
            }
        }
		
        internal Entry Put(object obj)
        {
            Entry[] tab = table;
            int hashcode = (int)(GetIdentityHashCode(obj) % tab.Length);
            for (Entry e = tab[hashcode], prev = null; e != null; e = e.next)
            {
                object target = e.wref.Target;
                if (target == null) 
                { 
                    if (prev != null)
                    {
                        prev.next = e.next;
                    }
                    else
                    {
                        tab[hashcode] = e.next;
                    }
                    e.clear();
                    count -= 1;
                }  
                else if (Object.ReferenceEquals(target, obj))
                {
                    return e;
                }
                else
                {
                    prev = e;
                }
            }
            if (count >= threshold)
            {
                // Rehash the table if the threshold is exceeded
                rehash();
                tab = table;
                hashcode = (int)(GetIdentityHashCode(obj) % tab.Length);
            }

            // Creates the new entry.
            count++;
            return tab[hashcode] = new Entry(obj, tab[hashcode]);
        }

        internal void SetOid(object obj, int oid)
        {
            lock(this)
            {  
                Entry e = Put(obj);
                e.oid = oid;
            }
        }
		
        internal void SetState(object obj, Persistent.ObjectState state)
        {
            lock(this)
            {  
                Entry e = Put(obj);
                e.state = state;              
                if ((state & Persistent.ObjectState.DIRTY) != 0)
                {
                    e.pin = obj;
                } 
                else
                {
                    e.pin = null;
                }
            }            
        }
		
        internal Entry Get(object obj)
        {
            if (obj != null)
            {
                Entry[] tab = table;
                int hashcode = (int)(GetIdentityHashCode(obj) % tab.Length);
                for (Entry e = tab[hashcode]; e != null; e = e.next)
                {
                    object target = e.wref.Target;
                    if (Object.ReferenceEquals(target, obj))
                    {
                        return e;
                    }
                }
            }
            return null;
        }

        internal int GetOid(object obj)
        {
            lock(this)
            {  
                Entry e = Get(obj);
                return e != null ? e.oid : 0;
            }
        }

        internal Persistent.ObjectState GetState(object obj)
        {
            lock(this)
            {  
                Entry e = Get(obj);
                return e != null ? e.state : Persistent.ObjectState.DELETED;
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
                    if (!e.wref.IsAlive) 
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
                return;
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
                    object target = e.wref.Target;
                    if (target != null) 
                    {
                        int hashcode = (int)(GetIdentityHashCode(target) % newCapacity);
                        e.next = newMap[hashcode];
                        newMap[hashcode] = e;
                    } 
                    else 
                    {
                        e.clear();
                        count -= 1;
                    }
                }
            }
	}	

        internal class Entry
        {
            internal Entry next;
            internal WeakReference wref;
            internal object pin;
            internal int oid;
            internal Persistent.ObjectState state;
		
            internal void clear() 
            { 
                wref = null;
                state = 0;
                next = null;
                pin = null;
            }

            internal Entry(object obj, Entry chain)
            {
                wref = new WeakReference(obj);
                next = chain;                
            }
        }
    }
}


