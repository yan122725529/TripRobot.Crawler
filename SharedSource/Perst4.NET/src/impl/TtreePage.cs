using System;
#if USE_GENERICS
using System.Collections.Generic;
#else
using System.Collections;
#endif

namespace Perst.Impl 
{
    [Serializable]
#if USE_GENERICS
    internal class TtreePage<K,V> : Persistent where V:class  
#else
    internal class TtreePage : Persistent  
#endif
    { 
        const int maxItems = (Page.pageSize-ObjectHeader.Sizeof-4*5)/4;
        const int minItems = maxItems - 2; // minimal number of items in internal node

#if USE_GENERICS
        internal TtreePage<K,V> left;
        internal TtreePage<K,V> right;
        internal int            balance;
        internal int            nItems;
        internal V[]            item;
#else
        internal TtreePage      left;
        internal TtreePage      right;
        internal int            balance;
        internal int            nItems;
        internal object[]  item;
#endif

        public override bool RecursiveLoading() 
        {
            return false;
        }

        internal TtreePage() {}

#if USE_GENERICS
        internal TtreePage(Storage db, V mbr) : base(db)
        { 
            nItems = 1;
            item = new V[maxItems];
            item[0] = mbr;
        }
#else
        internal TtreePage(Storage db, object mbr) : base(db) 
        { 
            nItems = 1;
            item = new object[maxItems];
            item[0] = mbr;
        }
#endif

#if USE_GENERICS
        V loadItem(int i) 
        { 
            V mbr = item[i];
            Storage.Load(mbr);
            return mbr;
        }
#else
        object loadItem(int i) 
        { 
            object mbr = item[i];
            Storage.Load(mbr);
            return mbr;
        }
#endif

#if USE_GENERICS
        internal bool find(PersistentComparator<K,V> comparator, K minValue, BoundaryKind minBoundary, K maxValue, BoundaryKind maxBoundary, List<V> selection)
#else
        internal bool find(PersistentComparator comparator, object minValue, BoundaryKind minBoundary, object maxValue, BoundaryKind maxBoundary, ArrayList selection)
#endif
        { 
            int l, r, m, n;
            Load();
            n = nItems;
            if (minBoundary != BoundaryKind.None) 
            { 
                if (-comparator.CompareMemberWithKey(loadItem(0), minValue) >= (int)minBoundary) 
                {	    
                    if (-comparator.CompareMemberWithKey(loadItem(n-1), minValue) >= (int)minBoundary) 
                    { 
                        if (right != null) 
                        { 
                            return right.find(comparator, minValue, minBoundary, maxValue, maxBoundary, selection); 
                        } 
                        return true;
                    }
                    for (l = 0, r = n; l < r;) 
                    { 
                        m = (l + r) >> 1;
                        if (-comparator.CompareMemberWithKey(loadItem(m), minValue) >= (int)minBoundary) 
                        {
                            l = m+1;
                        } 
                        else 
                        { 
                            r = m;
                        }
                    }
                    while (r < n) 
                    { 
                        if (maxBoundary != BoundaryKind.None
                            && comparator.CompareMemberWithKey(loadItem(r), maxValue) >= (int)maxBoundary)
                        { 
                            return false;
                        }
                        selection.Add(loadItem(r));
                        r += 1;
                    }
                    if (right != null) 
                    { 
                        return right.find(comparator, minValue, minBoundary, maxValue, maxBoundary, selection); 
                    } 
                    return true;	
                }
            }	
            if (left != null) 
            { 
                if (!left.find(comparator, minValue, minBoundary, maxValue, maxBoundary, selection)) 
                { 
                    return false;
                }
            }
            for (l = 0; l < n; l++) 
            { 
                if (maxBoundary != BoundaryKind.None 
                    && comparator.CompareMemberWithKey(loadItem(l), maxValue) >= (int)maxBoundary) 
                {
                    return false;
                }
                selection.Add(loadItem(l));
            }
            if (right != null) 
            { 
                return right.find(comparator, minValue, minBoundary, maxValue, maxBoundary, selection);
            }         
            return true;
        }
    
#if USE_GENERICS
        internal bool contains(PersistentComparator<K,V> comparator, V mbr)
#else
        internal bool contains(PersistentComparator comparator, object mbr)
#endif
        { 
            int l, r, m, n;
            Load();
            n = nItems;
            if (comparator.CompareMembers(loadItem(0), mbr) < 0) 
            {	    
                if (comparator.CompareMembers(loadItem(n-1), mbr) < 0) 
                { 
                    if (right != null) 
                    { 
                        return right.contains(comparator, mbr); 
                    } 
                    return false;
                }
                for (l = 0, r = n; l < r;) 
                { 
                    m = (l + r) >> 1;
                    if (comparator.CompareMembers(loadItem(m), mbr) < 0) 
                    {
                        l = m+1;
                    } 
                    else 
                    { 
                        r = m;
                    }
                }
                while (r < n) 
                { 
                    if (mbr == loadItem(r)) 
                    { 
                        return true;
                    }
                    if (comparator.CompareMembers(item[r], mbr) > 0) 
                    { 
                        return false;
                    }
                    r += 1;
                }
                if (right != null) 
                { 
                    return right.contains(comparator, mbr); 
                } 
                return false;	
            }
            if (left != null) 
            { 
                if (left.contains(comparator, mbr)) 
                { 
                    return true;
                }
            }
            for (l = 0; l < n; l++) 
            { 
                if (mbr == loadItem(l)) 
                { 
                    return true;
                }
                if (comparator.CompareMembers(item[l], mbr) > 0) 
                {
                    return false;
                }
            }
            if (right != null) 
            { 
                return right.contains(comparator, mbr);
            }         
            return false;
        }

    
        internal const int OK         = 0;
        internal const int NOT_UNIQUE = 1;
        internal const int NOT_FOUND  = 2;
        internal const int OVERFLOW   = 3;
        internal const int UNDERFLOW  = 4;

#if USE_GENERICS
        internal int insert(PersistentComparator<K,V> comparator, V mbr, bool unique, ref TtreePage<K,V> pgRef) 
        { 
            TtreePage<K,V> pg, lp, rp;
            V reinsertItem;
#else
        internal int insert(PersistentComparator comparator, object mbr, bool unique, ref TtreePage pgRef) 
        { 
            TtreePage pg, lp, rp;
            object reinsertItem;
#endif
            Load();
            int n = nItems;
            int diff = comparator.CompareMembers(mbr, loadItem(0));
            if (diff <= 0) 
            { 
                if (unique && diff == 0) 
                { 
                    return NOT_UNIQUE;
                }
                if ((left == null || diff == 0) && n != maxItems) 
                { 
                    Modify();
                    //for (int i = n; i > 0; i--) item[i] = item[i-1];
                    Array.Copy(item, 0, item, 1, n);
                    item[0] = mbr;
                    nItems += 1;
                    return OK;
                } 
                if (left == null) 
                { 
                    Modify();
#if USE_GENERICS
                    left = new TtreePage<K,V>(Storage, mbr);
#else
                    left = new TtreePage(Storage, mbr);
#endif
                } 
                else 
                {
                    pg = pgRef;
                    pgRef = left;
                    int result = left.insert(comparator, mbr, unique, ref pgRef);
                    if (result == NOT_UNIQUE) 
                    { 
                        return NOT_UNIQUE;
                    }
                    Modify();
                    left = pgRef;
                    pgRef = pg;
                    if (result == OK) return OK;
                }
                if (balance > 0) 
                { 
                    balance = 0;
                    return OK;
                } 
                else if (balance == 0) 
                { 
                    balance = -1;
                    return OVERFLOW;
                } 
                else 
                { 
                    lp = this.left;
                    lp.Load();
                    lp.Modify();
                    if (lp.balance < 0) 
                    { // single LL turn
                        this.left = lp.right;
                        lp.right = this;
                        balance = 0;
                        lp.balance = 0;
                        pgRef = lp;
                    } 
                    else 
                    { // double LR turn
                        rp = lp.right;
                        rp.Load();
                        rp.Modify();
                        lp.right = rp.left;
                        rp.left = lp;
                        this.left = rp.right;
                        rp.right = this;
                        balance = (rp.balance < 0) ? 1 : 0;
                        lp.balance = (rp.balance > 0) ? -1 : 0;
                        rp.balance = 0;
                        pgRef = rp;
                    }
                    return OK;
                }
            } 
            diff = comparator.CompareMembers(mbr, loadItem(n-1));
            if (diff >= 0) 
            { 
                if (unique && diff == 0) 
                { 
                    return NOT_UNIQUE;
                }
                if ((right == null || diff == 0) && n != maxItems) 
                { 
                    Modify();
                    item[n] = mbr;
                    nItems += 1;
                    return OK;
                }
                if (right == null) 
                { 
                    Modify();
#if USE_GENERICS
                    right = new TtreePage<K,V>(Storage, mbr);
#else
                    right = new TtreePage(Storage, mbr);
#endif
                } 
                else 
                { 
                    pg = pgRef;
                    pgRef = right;
                    int result = right.insert(comparator, mbr, unique, ref pgRef);
                    if (result == NOT_UNIQUE) 
                    { 
                        return NOT_UNIQUE;
                    }
                    Modify();
                    right = pgRef;
                    pgRef = pg;
                    if (result == OK) return OK;
                }
                if (balance < 0) 
                { 
                    balance = 0;
                    return OK;
                } 
                else if (balance == 0) 
                { 
                    balance = 1;
                    return OVERFLOW;
                } 
                else 
                { 
                    rp = this.right;
                    rp.Load();
                    rp.Modify();
                    if (rp.balance > 0) 
                    { // single RR turn
                        this.right = rp.left;
                        rp.left = this;
                        balance = 0;
                        rp.balance = 0;
                        pgRef = rp;
                    } 
                    else 
                    { // double RL turn
                        lp = rp.left;
                        lp.Load();
                        lp.Modify();
                        rp.left = lp.right;
                        lp.right = rp;
                        this.right = lp.left;
                        lp.left = this;
                        balance = (lp.balance > 0) ? -1 : 0;
                        rp.balance = (lp.balance < 0) ? 1 : 0;
                        lp.balance = 0;
                        pgRef = lp;
                    }
                    return OK;
                }
            }
            int l = 1, r = n-1;
            while (l < r)  
            {
                int i = (l+r) >> 1;
                diff = comparator.CompareMembers(mbr, loadItem(i));
                if (diff > 0) 
                { 
                    l = i + 1;
                } 
                else 
                { 
                    r = i;
                    if (diff == 0) 
                    { 
                        if (unique) 
                        { 
                            return NOT_UNIQUE;
                        }
                        break;
                    }
                }
            }
            // Insert before item[r]
            Modify();
            if (n != maxItems) 
            {
                Array.Copy(item, r, item, r+1, n-r);
                //for (int i = n; i > r; i--) item[i] = item[i-1]; 
                item[r] = mbr;
                nItems += 1;
                return OK;
            } 
            else 
            { 
                if (balance >= 0) 
                { 
                    reinsertItem = loadItem(0);
                    Array.Copy(item, 1, item, 0, r-1);
                    //for (int i = 1; i < r; i++) item[i-1] = item[i]; 
                    item[r-1] = mbr;
                } 
                else 
                { 
                    reinsertItem = loadItem(n-1);
                    Array.Copy(item, r, item, r+1, n-r-1);
                    //for (int i = n-1; i > r; i--) item[i] = item[i-1]; 
                    item[r] = mbr;
                }
                return insert(comparator, reinsertItem, unique, ref pgRef);
            }
        }
       
#if USE_GENERICS
        internal int balanceLeftBranch(ref TtreePage<K,V> pgRef) 
        {
            TtreePage<K,V> lp, rp;
#else
        internal int balanceLeftBranch(ref TtreePage pgRef) 
        {
            TtreePage lp, rp;
#endif
            if (balance < 0) 
            { 
                balance = 0;
                return UNDERFLOW;
            } 
            else if (balance == 0) 
            { 
                balance = 1;
                return OK;
            } 
            else 
            { 
                rp = this.right;
                rp.Load();
                rp.Modify();
                if (rp.balance >= 0) 
                { // single RR turn
                    this.right = rp.left;
                    rp.left = this;
                    if (rp.balance == 0) 
                    { 
                        this.balance = 1;
                        rp.balance = -1;
                        pgRef = rp;
                        return OK;
                    } 
                    else 
                    { 
                        balance = 0;
                        rp.balance = 0;
                        pgRef = rp;
                        return UNDERFLOW;
                    }
                } 
                else 
                { // double RL turn
                    lp = rp.left;
                    lp.Load();
                    lp.Modify();
                    rp.left = lp.right;
                    lp.right = rp;
                    this.right = lp.left;
                    lp.left = this;
                    balance = lp.balance > 0 ? -1 : 0;
                    rp.balance = lp.balance < 0 ? 1 : 0;
                    lp.balance = 0;
                    pgRef = lp;
                    return UNDERFLOW;
                }
            }
        }

#if USE_GENERICS
        internal int balanceRightBranch(ref TtreePage<K,V> pgRef) 
        {
            TtreePage<K,V> lp, rp;
#else
        internal int balanceRightBranch(ref TtreePage pgRef) 
        {
            TtreePage lp, rp;
#endif
            if (balance > 0) 
            { 
                balance = 0;
                return UNDERFLOW;
            } 
            else if (balance == 0) 
            { 
                balance = -1;
                return OK;
            } 
            else 
            { 
                lp = this.left;
                lp.Load();
                lp.Modify();
                if (lp.balance <= 0) 
                { // single LL turn
                    this.left = lp.right;
                    lp.right = this;
                    if (lp.balance == 0) 
                    { 
                        balance = -1;
                        lp.balance = 1;
                        pgRef = lp;
                        return OK;
                    } 
                    else 
                    { 
                        balance = 0;
                        lp.balance = 0;
                        pgRef = lp;
                        return UNDERFLOW;
                    }
                } 
                else 
                { // double LR turn
                    rp = lp.right;
                    rp.Load();
                    rp.Modify();
                    lp.right = rp.left;
                    rp.left = lp;
                    this.left = rp.right;
                    rp.right = this;
                    balance = rp.balance < 0 ? 1 : 0;
                    lp.balance = rp.balance > 0 ? -1 : 0;
                    rp.balance = 0;
                    pgRef = rp;
                    return UNDERFLOW;
                }
            }
        }
    
#if USE_GENERICS
        internal int remove(PersistentComparator<K,V> comparator, V mbr, ref TtreePage<K,V> pgRef)
        {
            TtreePage<K,V> pg, next, prev;
#else
        internal int remove(PersistentComparator comparator, object mbr, ref TtreePage pgRef)
        {
            TtreePage pg, next, prev;
#endif
            Load();
            int n = nItems;
            int diff = comparator.CompareMembers(mbr, loadItem(0));
            if (diff <= 0) 
            { 
                if (left != null) 
                { 
                    Modify();
                    pg = pgRef;
                    pgRef = left;
                    int h = left.remove(comparator, mbr, ref pgRef);
                    left = pgRef;
                    pgRef = pg;
                    if (h == UNDERFLOW) 
                    { 
                        return balanceLeftBranch(ref pgRef);
                    } 
                    else if (h == OK) 
                    { 
                        return OK;
                    }
                }
            }
            diff = comparator.CompareMembers(mbr, loadItem(n-1));
            if (diff <= 0) 
            {	    
                for (int i = 0; i < n; i++) 
                { 
                    if (item[i] == mbr) 
                    { 
                        if (n == 1) 
                        { 
                            if (right == null) 
                            { 
                                Deallocate();
                                pgRef = left;
                                return UNDERFLOW;
                            } 
                            else if (left == null) 
                            { 
                                Deallocate();
                                pgRef = right;
                                return UNDERFLOW;
                            } 
                        }
                        Modify();
                        if (n <= minItems) 
                        { 
                            if (left != null && balance <= 0) 
                            {  
                                prev = left;
                                prev.Load();
                                while (prev.right != null) 
                                {                                 
                                    prev = prev.right;
                                    prev.Load();
                                }
                                Array.Copy(item, 0, item, 1, i);
                                //while (--i >= 0) 
                                //{ 
                                //    item[i+1] = item[i];
                                //}
                                item[0] = prev.item[prev.nItems-1];
                                pg = pgRef;
                                pgRef = left;
                                int h = left.remove(comparator, loadItem(0), ref pgRef);
                                left = pgRef;
                                pgRef = pg;
                                if (h == UNDERFLOW) 
                                {
                                    h = balanceLeftBranch(ref pgRef);
                                }
                                return h;
                            } 
                            else if (right != null) 
                            { 
                                next = right;
                                next.Load();
                                while (next.left != null) 
                                { 
                                    next = next.left;
                                    next.Load();
                                }
                                Array.Copy(item, i+1, item, i, n-i-1);
                                //while (++i < n) 
                                //{ 
                                //    item[i-1] = item[i];
                                //}
                                item[n-1] = next.item[0];
                                pg = pgRef;
                                pgRef = right;
                                int h = right.remove(comparator, loadItem(n-1), ref pgRef);
                                right = pgRef;
                                pgRef = pg;
                                if (h == UNDERFLOW) 
                                {
                                    h = balanceRightBranch(ref pgRef);
                                }
                                return h;
                            }
                        }
                        Array.Copy(item, i+1, item, i, n-i-1);
                        //while (++i < n) 
                        //{ 
                        //    item[i-1] = item[i];
                        //}
                        item[n-1] = null;
                        nItems -= 1;
                        return OK;
                    }
                }
            }
            if (right != null) 
            { 
                Modify();
                pg = pgRef;
                pgRef = right;
                int h = right.remove(comparator, mbr, ref pgRef);
                right = pgRef;
                pgRef = pg;
                if (h == UNDERFLOW) 
                { 
                    return balanceRightBranch(ref pgRef);
                }
                else 
                { 
                    return h;
                }
            }
            return NOT_FOUND;
        }


        internal int toArray(object[] arr, int index) 
        { 
            Load();
            if (left != null) 
            { 
                index = left.toArray(arr, index);
            }
            for (int i = 0, n = nItems; i < n; i++) 
            { 
                arr[index++] = loadItem(i);
            }
            if (right != null) 
            { 
                index = right.toArray(arr, index);
            }
            return index;
        }

        internal void prune() 
        { 
            Load();
            if (left != null) 
            { 
                left.prune();
            }
            if (right != null) 
            { 
                right.prune();
            }
            Deallocate();
        }

    }
}
