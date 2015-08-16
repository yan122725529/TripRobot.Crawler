namespace Perst.Impl
{
    using System;
    using System.Collections;
    using Perst;
    using System.Diagnostics;
#if USE_GENERICS
    using Link = Perst.Link<object>;
#endif

    [Serializable]
    internal class RtreeRnPage:Persistent,ISelfSerializable
    {
        internal int           n;
        internal int           card;
        internal RectangleRn[] b;
        internal Link          branch;

        public void Pack(ObjectWriter writer)
        {
            int nDims = ((Page.pageSize-ObjectHeader.Sizeof-12)/card - 4) / 16;
            writer.Write(n);
            writer.WriteObject(branch);
            for (int i = 0; i < n; i++) {
                for (int j = 0; j < nDims; j++) {
                    writer.Write(b[i].GetMinCoord(j));
                    writer.Write(b[i].GetMaxCoord(j));
                }
            }
        }

        public void Unpack(ObjectReader reader)
        {
            n = reader.ReadInt32();
            branch = (Link)reader.ReadObject();
            card = branch.Count;
            int nDims = ((Page.pageSize-ObjectHeader.Sizeof-12)/card - 4) / 16;
            double[] coords = new double[nDims*2];
            b = new RectangleRn[card];
            for (int i = 0; i < n; i++) {
                for (int j = 0; j < nDims; j++) {
                    coords[j] = reader.ReadDouble();
                    coords[j+nDims] = reader.ReadDouble();
                }
                b[i] = new RectangleRn(coords);
            }
        }

        internal RtreeRnPage(Storage storage, object obj, RectangleRn r)
        {
            branch = storage.CreateLink(card);
            branch.Length = card;
            b = new RectangleRn[card];
            setBranch(0, new RectangleRn(r), obj);
            n = 1;
        }

        internal RtreeRnPage(Storage storage, RtreeRnPage root, RtreeRnPage p)
        {
            branch = storage.CreateLink(card);
            branch.Length = card;
            b = new RectangleRn[card];
            n = 2;
            setBranch(0, root.cover(), root);
            setBranch(1, p.cover(), p);
        }

        internal RtreeRnPage() {}

        internal RtreeRnPage insert(Storage storage, RectangleRn r, object obj, int level)
        {
            Modify();
            if (--level != 0)
            {
                // not leaf page
                int i, mini = 0;
                double minIncr = Double.MaxValue;
                double minArea = Double.MaxValue;
                for (i = 0; i < n; i++)
                {
                    double area = b[i].Area();
                    double incr = RectangleRn.JoinArea(b[i], r) - area;
                    if (incr < minIncr)
                    {
                        minIncr = incr;
                        minArea = area;
                        mini = i;
                    }
                    else if (incr == minIncr && area < minArea)
                    {
                        minArea = area;
                        mini = i;
                    }
                }
                RtreeRnPage p = (RtreeRnPage)branch[mini];
                RtreeRnPage q = p.insert(storage, r, obj, level);
                if (q == null)
                {
                    // child was not split
                    b[mini].Join(r);
                    return null;
                }
                else
                {
                    // child was split
                    setBranch(mini, p.cover(),  p);
                    return addBranch(storage, q.cover(), q);
                }
            }
            else
            {
                return addBranch(storage, new RectangleRn(r), obj);
            }
        }

        internal int remove(RectangleRn r, object obj, int level, ArrayList reinsertList)
        {
            if (--level != 0)
            {
                for (int i = 0; i < n; i++)
                {
                    if (r.Intersects(b[i]))
                    {
                        RtreeRnPage pg = (RtreeRnPage)branch[i];
                        int reinsertLevel = pg.remove(r, obj, level, reinsertList);
                        if (reinsertLevel >= 0)
                        {
                            if (pg.n >= card/2)
                            {
                                setBranch(i, pg.cover(), pg);
                                Modify();
                            }
                            else
                            {
                                // not enough entries in child
                                reinsertList.Add(pg);
                                reinsertLevel = level - 1;
                                removeBranch(i);
                            }
                            return reinsertLevel;
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    if (branch.ContainsElement(i, obj))
                    {
                        removeBranch(i);
                        return 0;
                    }
                }
            }
            return -1;
        }

        internal void find(RectangleRn r, ArrayList result, int level)
        {
            if (--level != 0)
            { /* this is an internal node in the tree */
                for (int i = 0; i < n; i++)
                {
                    if (r.Intersects(b[i]))
                    {
                        ((RtreeRnPage)branch[i]).find(r, result, level);
                    }
                }
            }
            else
            { /* this is a leaf node */
                for (int i = 0; i < n; i++)
                {
                    if (r.Intersects(b[i]))
                    {
                        result.Add(branch[i]);
                    }
                }
            }
        }

        internal void purge(int level)
        {
            if (--level != 0)
            { /* this is an internal node in the tree */
                for (int i = 0; i < n; i++)
                {
                    ((RtreeRnPage)branch[i]).purge(level);
                }
            }
            Deallocate();
        }

        void setBranch(int i, RectangleRn r, object obj)
        {
            b[i] = r;
            branch[i] = obj;
        }

        void removeBranch(int i)
        {
            n -= 1;
            Array.Copy(b, i+1, b, i, n-i);
            branch.Remove(i);
            branch.Length = card;
            Modify();
        }

        RtreeRnPage addBranch(Storage storage, RectangleRn r, object obj)
        {
            if (n < card)
            {
                setBranch(n++, r, obj);
                return null;
            }
            else
            {
                return splitPage(storage, r, obj);
            }
        }

        RtreeRnPage splitPage(Storage storage, RectangleRn r, object obj)
        {
            int i, j, seed0 = 0, seed1 = 0;
            double[] rectArea = new double[card+1];
            double   waste;
            double   worstWaste = Double.MinValue;
            //
            // As the seeds for the two groups, find two rectangles which waste
            // the most area if covered by a single rectangle.
            //
            rectArea[0] = r.Area();
            for (i = 0; i < card; i++)
            {
                rectArea[i+1] = b[i].Area();
            }
            RectangleRn bp = r;
            for (i = 0; i < card; i++)
            {
                for (j = i+1; j <= card; j++)
                {
                    waste = RectangleRn.JoinArea(bp, b[j-1]) - rectArea[i] - rectArea[j];
                    if (waste > worstWaste)
                    {
                        worstWaste = waste;
                        seed0 = i;
                        seed1 = j;
                    }
                }
                bp = b[i];
            }
            byte[] taken = new byte[card];
            RectangleRn group0, group1;
            double      groupArea0, groupArea1;
            int         groupCard0, groupCard1;
            RtreeRnPage pg;

            taken[seed1-1] = 2;
            group1 = new RectangleRn(b[seed1-1]);

            if (seed0 == 0)
            {
                group0 = new RectangleRn(r);
                pg = new RtreeRnPage(storage, obj, r);
            }
            else
            {
                group0 = new RectangleRn(b[seed0-1]);
                pg = new RtreeRnPage(storage, branch.GetRaw(seed0-1), group0);
                setBranch(seed0-1, r, obj);
            }
            groupCard0 = groupCard1 = 1;
            groupArea0 = rectArea[seed0];
            groupArea1 = rectArea[seed1];
            //
            // Split remaining rectangles between two groups.
            // The one chosen is the one with the greatest difference in area
            // expansion depending on which group - the rect most strongly
            // attracted to one group and repelled from the other.
            //
            while (groupCard0 + groupCard1 < card + 1
                && groupCard0 < card + 1 - card/2
                && groupCard1 < card + 1 - card/2)
            {
                int betterGroup = -1, chosen = -1;
                double biggestDiff = -1;
                for (i = 0; i < card; i++)
                {
                    if (taken[i] == 0)
                    {
                        double diff = (RectangleRn.JoinArea(group0, b[i]) - groupArea0)
                            - (RectangleRn.JoinArea(group1, b[i]) - groupArea1);
                        if (diff > biggestDiff || -diff > biggestDiff)
                        {
                            chosen = i;
                            if (diff < 0)
                            {
                                betterGroup = 0;
                                biggestDiff = -diff;
                            }
                            else
                            {
                                betterGroup = 1;
                                biggestDiff = diff;
                            }
                        }
                    }
                }
                Debug.Assert(chosen >= 0);
                if (betterGroup == 0)
                {
                    group0.Join(b[chosen]);
                    groupArea0 = group0.Area();
                    taken[chosen] = 1;
                    pg.setBranch(groupCard0++, b[chosen], branch.GetRaw(chosen));
                }
                else
                {
                    groupCard1 += 1;
                    group1.Join(b[chosen]);
                    groupArea1 = group1.Area();
                    taken[chosen] = 2;
                }
            }
            //
            // If one group gets too full, then remaining rectangle are
            // split between two groups in such way to balance cards of two groups.
            //
            if (groupCard0 + groupCard1 < card + 1)
            {
                for (i = 0; i < card; i++)
                {
                    if (taken[i] == 0)
                    {
                        if (groupCard0 >= groupCard1)
                        {
                            taken[i] = 2;
                            groupCard1 += 1;
                        }
                        else
                        {
                            taken[i] = 1;
                            pg.setBranch(groupCard0++, b[i], branch.GetRaw(i));
                        }
                    }
                }
            }
            pg.n = groupCard0;
            n = groupCard1;
            for (i = 0, j = 0; i < groupCard1; j++)
            {
                if (taken[j] == 2)
                {
                    setBranch(i++, b[j], branch.GetRaw(j));
                }
            }
            // truncate rest of link
            branch.Length = groupCard1;
            branch.Length = card;
            return pg;
        }

        internal RectangleRn cover()
        {
            RectangleRn r = new RectangleRn(b[0]);
            for (int i = 1; i < n; i++)
            {
                r.Join(b[i]);
            }
            return r;
        }
    }
}