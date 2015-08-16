namespace Perst
{
    using System;
    using System.Threading;
    using System.Collections;
    using System.Diagnostics;
	
    /// <summary>Base class for persistent capable objects supporting locking
    /// </summary>
    [Serializable]
    public class PersistentResource : Persistent, IResource 
    {
#if COMPACT_NET_FRAMEWORK
        enum LockMode
        {
            Shared,
            Update,
            Exclusive
        };
        
        class WaitContext 
        {
            internal AutoResetEvent evt;
            internal WaitContext    next;
            internal LockMode       mode;

            internal WaitContext() 
            {
                evt = new AutoResetEvent(false);
            }
        }
        
        static WaitContext freeContexts;

        [NonSerialized()]
        WaitContext queueStart;
        [NonSerialized()]
        WaitContext queueEnd;

        private void wait(LockMode mode)
        {
            WaitContext ctx;
            lock (typeof(PersistentResource)) 
            {
                ctx = freeContexts;
                if (ctx == null) 
                {
                    ctx = new WaitContext();
                } 
                else 
                {
                    freeContexts = ctx.next;
                }
                ctx.next = null;
            }
            if (queueStart != null) 
            { 
                queueEnd = queueEnd.next = ctx;
            } 
            else 
            { 
                queueStart = queueEnd = ctx;
            }                            
            ctx.mode = mode;
 
            Monitor.Exit(this);
            ctx.evt.WaitOne();

            lock (typeof(PersistentResource)) 
            {
                ctx.next = freeContexts;
                freeContexts = ctx;
            }
        }

        public void SharedLock()    
        {
            Monitor.Enter(this);
            Thread currThread = Thread.CurrentThread;
            if (owner == currThread) 
            { 
                if (nWriters != 0) { 
                    nWriters += 1;
                } else {
                    Debug.Assert(nUpdaters != 0);
                    nUpdaters += 1;
                }
                Monitor.Exit(this);
            } 
            else if (nWriters == 0) 
            { 
                if (storage == null || storage.lockObject(this)) 
                { 
                    nReaders += 1;
                }
                Monitor.Exit(this);
            } 
            else 
            { 
                wait(LockMode.Shared);
                if (storage != null) 
                { 
                    storage.lockObject(this);
                }
            } 
        }
                    
                    
        public void ExclusiveLock() 
        {
            Thread currThread = Thread.CurrentThread;
            Monitor.Enter(this);
            if (owner == currThread) 
            { 
                nWriters += nUpdaters + 1;
                nUpdaters = 0;
                Monitor.Exit(this);
            } 
            else if (nReaders == 0 && nUpdaters == 0 && nWriters == 0) 
            { 
                nWriters = 1;
                owner = currThread;
                if (storage != null) 
                { 
                    storage.lockObject(this);
                }
                Monitor.Exit(this);
            } 
            else {
                wait(LockMode.Exclusive);
                owner = currThread;    
                if (storage != null) 
                { 
                    storage.lockObject(this);
                }
            } 
        }
                    
        public void UpdateLock() 
        {
            Thread currThread = Thread.CurrentThread;
            Monitor.Enter(this);
            if (owner == currThread) 
            { 
                if (nWriters != 0) { 
                    nWriters += 1;
                } else {
                    Debug.Assert(nUpdaters != 0);
                    nUpdaters += 1;
                }
                Monitor.Exit(this);
            } 
            else if (nUpdaters == 0 && nWriters == 0) 
            { 
                nUpdaters = 1;
                owner = currThread;
                if (storage != null) 
                { 
                    storage.lockObject(this);
                }
                Monitor.Exit(this);
            } 
            else {
                wait(LockMode.Update);
                owner = currThread;    
                if (storage != null) 
                { 
                    storage.lockObject(this);
                }
            } 
        }
                    

        private void notify() 
        {
            WaitContext next, ctx = queueStart;
            while (ctx != null) 
            { 
                switch (ctx.mode) 
                {
                    case LockMode.Exclusive:
                        if (nWriters == 0 && nUpdaters == 0 && nReaders == 0) 
                        {
                            nWriters = 1;
                            next = ctx.next;
                            ctx.evt.Set();
                            ctx = next;
                        } 
                        break;
                    case LockMode.Shared:
                        if (nWriters == 0)
                        {
                            nReaders += 1;
                            next = ctx.next;
                            ctx.evt.Set();
                            ctx = next;
                            continue;
                        } 
                        break;
                    case LockMode.Update:
                        if (nUpdaters == 0 && nWriters == 0)
                        {
                            nUpdaters = 1;
                            next = ctx.next;
                            ctx.evt.Set();
                            ctx = next;
                            continue;
                        } 
                        break;
                }
                break;
            }
            queueStart = ctx;
        }

        
        public void Unlock() 
        {
            lock (this) 
            { 
                if (nWriters != 0) 
                { 
                    if (--nWriters == 0) 
                    { 
                        owner = null;
                        notify();
                    }
                } 
                else if (nUpdaters != 0) 
                { 
                    if (--nUpdaters == 0) 
                    { 
                        owner = null;
                        notify();
                    }
                } 
                else if (nReaders != 0)
                { 
                    if (--nReaders == 0) 
                    { 
                        notify();
                    }
                }
            }
        }

        public void Reset() 
        { 
            lock (this) 
            { 
                if (nWriters + nUpdaters != 0) 
                { 
                    Debug.Assert(owner != null);
                    Debug.Assert(nReaders == 0);
                    nWriters = 0;
                    nUpdaters = 0;
                    owner = null;
                } 
                else if (nReaders > 0) 
                { 
                    nReaders -= 1;
                }
                notify();
            }
        }
#else
        public void SharedLock()    
        {
            lock (this) 
            { 
                Thread currThread = Thread.CurrentThread;
                while (true) 
                { 
                    if (owner == currThread) 
                    { 
                        if (nWriters != 0) { 
                            nWriters += 1;
                        } else {
                            Debug.Assert(nUpdaters != 0);
                            nUpdaters += 1;
                        }
                        break;
                    } 
                    else if (nWriters == 0) 
                    { 
                        if (storage == null || storage.lockObject(this)) 
                        {                        
                            nReaders += 1;
                        }
                        break;
                    } 
                    else 
                    { 
                        Monitor.Wait(this);
                    }
                }
            }
        }
                    
        public bool SharedLock(long timeout) 
        {
            Thread currThread = Thread.CurrentThread;
            DateTime startTime = DateTime.Now;
            TimeSpan ts = TimeSpan.FromMilliseconds(timeout);
            lock (this) 
            { 
                while (true) 
                { 
                    if (owner == currThread) 
                    { 
                        if (nWriters != 0) { 
                            nWriters += 1;
                        } else {
                            Debug.Assert(nUpdaters != 0);
                            nUpdaters += 1;
                        }
                        return true;
                    } 
                    else if (nWriters == 0) 
                    { 
                        if (storage == null || storage.lockObject(this)) 
                        {                        
                            nReaders += 1;
                        }
                        return true;
                    } 
                    else 
                    { 
                        DateTime currTime = DateTime.Now;
                        if (startTime + ts <= currTime) 
                        { 
                            return false;
                        }
                        Monitor.Wait(this, startTime + ts - currTime);
                    }
                }
            } 
        }
    
                    
        public void ExclusiveLock() 
        {
            Thread currThread = Thread.CurrentThread;
            lock (this)
            { 
                while (true) 
                { 
                    if (owner == currThread) 
                    { 
                        nWriters += nUpdaters + 1;
                        nUpdaters = 0;
                        break;
                    } 
                    else if (nReaders == 0 && nUpdaters == 0 && nWriters == 0) 
                    { 
                        nWriters = 1;
                        owner = currThread;
                        if (storage != null) 
                        { 
                            storage.lockObject(this);
                        }
                        break;
                    } 
                    else 
                    { 
                        Monitor.Wait(this);
                    }
                }
            } 
        }
                    
        public bool ExclusiveLock(long timeout) 
        {
            Thread currThread = Thread.CurrentThread;
            TimeSpan ts = TimeSpan.FromMilliseconds(timeout);
            DateTime startTime = DateTime.Now;
            lock (this) 
            { 
                while (true) 
                { 
                    if (owner == currThread) 
                    { 
                        nWriters += nUpdaters + 1;
                        nUpdaters = 0;
                        return true;
                    } 
                    else if (nReaders == 0 && nUpdaters == 0 && nWriters == 0) 
                    { 
                        nWriters = 1;
                        owner = currThread;
                        if (storage != null) 
                        { 
                            storage.lockObject(this);
                        }
                        return true;
                    } 
                    else 
                    { 
                        DateTime currTime = DateTime.Now;
                        if (startTime + ts <= currTime) 
                        { 
                            return false;
                        }
                        Monitor.Wait(this, startTime + ts - currTime);
                    }
                }
            } 
        }

        public void UpdateLock() 
        {
            Thread currThread = Thread.CurrentThread;
            lock (this)
            { 
                while (true) 
                { 
                    if (owner == currThread) 
                    { 
                        if (nWriters != 0) { 
                            nWriters += 1;
                        } else {
                            Debug.Assert(nUpdaters != 0);
                            nUpdaters += 1;
                        }
                        break;
                    } 
                    else if (nUpdaters == 0 && nWriters == 0) 
                    { 
                        nUpdaters = 1;
                        owner = currThread;
                        if (storage != null) 
                        { 
                            storage.lockObject(this);
                        }
                        break;
                    } 
                    else 
                    { 
                        Monitor.Wait(this);
                    }
                }
            } 
        }
                    
        public bool UpdateLock(long timeout) 
        {
            Thread currThread = Thread.CurrentThread;
            TimeSpan ts = TimeSpan.FromMilliseconds(timeout);
            DateTime startTime = DateTime.Now;
            lock (this) 
            { 
                while (true) 
                { 
                    if (owner == currThread) 
                    { 
                        if (nWriters != 0) { 
                            nWriters += 1;
                        } else {
                            Debug.Assert(nUpdaters != 0);
                            nUpdaters += 1;
                        }
                        return true;
                    } 
                    else if (nUpdaters == 0 && nWriters == 0) 
                    { 
                        nUpdaters = 1;
                        owner = currThread;
                        if (storage != null) 
                        { 
                            storage.lockObject(this);
                        }
                        return true;
                    } 
                    else 
                    { 
                        DateTime currTime = DateTime.Now;
                        if (startTime + ts <= currTime) 
                        { 
                            return false;
                        }
                        Monitor.Wait(this, startTime + ts - currTime);
                    }
                }
            } 
        }

        public void Unlock() 
        {
            lock (this) 
            { 
                if (nWriters != 0) 
                { 
                    if (--nWriters == 0) 
                    { 
                        owner = null;
                        Monitor.PulseAll(this);
                    }
                } 
                else if (nUpdaters != 0) 
                { 
                    if (--nUpdaters == 0) 
                    { 
                        owner = null;
                        Monitor.PulseAll(this);
                    }
                } 
                else if (nReaders != 0)
                { 
                    if (--nReaders == 0) 
                    { 
                        Monitor.PulseAll(this);
                    }
                }
            }
        }

        public void Reset() 
        { 
            lock (this) 
            { 
                if (nWriters + nUpdaters != 0) 
                { 
                    Debug.Assert(owner != null);
                    Debug.Assert(nReaders == 0);
                    nWriters = 0;
                    nUpdaters = 0;
                    owner = null;
                } 
                else if (nReaders > 0) 
                { 
                    nReaders -= 1;
                }
                Monitor.PulseAll(this);
            }
        }

#endif
        internal protected PersistentResource() {}

        internal protected PersistentResource(Storage storage) 
            : base(storage) {}

        [NonSerialized()]
        private Thread owner;
        [NonSerialized()]
        private int    nReaders;
        [NonSerialized()]
        private int    nUpdaters;
        [NonSerialized()]
        private int    nWriters;
    }

    /// <summary>
    /// Helper class for exception safe locking of objects.
    /// It is intended to be used in USING statement:
    /// <code>
    ///     using (SharedLock lock = new SharedLock(obj)) 
    ///     { 
    ///         ... // do somthing with persistent object
    ///     }
    /// </code>    
    /// </summary>
    public class SharedLock : IDisposable
    {        
        public SharedLock(PersistentResource res)
        {
            resource = res;
            res.SharedLock();
        }
        
        public void Dispose() 
        {
            resource.Unlock();
        }

        private PersistentResource resource;
    } 
           
    /// <summary>
    /// Helper class for exception safe locking of objects.
    /// It is intended to be used in USING statement:
    /// <code>
    ///     using (UpdateLock lock = new UpdateLock(obj)) 
    ///     { 
    ///         ... // do somthing with persistent object
    ///     }
    /// </code>    
    /// </summary>
    public class UpdateLock : IDisposable
    {        
        public UpdateLock(PersistentResource res)
        {
            resource = res;
            res.UpdateLock();
        }
        
        public void Dispose() 
        {
            resource.Unlock();
        }

        private PersistentResource resource;
    } 
           
    /// <summary>
    /// Helper class for exception safe locking of objects.
    /// It is intended to be used in USING statement:
    /// <code>
    ///     using (ExclusiveLock lock = new ExclusiveLock(obj)) 
    ///     { 
    ///         ... // do somthing with persistent object
    ///     }
    /// </code>    
    /// </summary>
    public class ExclusiveLock : IDisposable
    {        
        public ExclusiveLock(PersistentResource res)
        {
            resource = res;
            res.ExclusiveLock();
        }
        
        public void Dispose() 
        {
            resource.Unlock();
        }

        private PersistentResource resource;
    } 
           
                
}
