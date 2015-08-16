using System;
using System.IO;
using System.Collections;

namespace Perst
{
    /// <summary>    
    /// Class used to profile query execution. It should be registered as storage listener.
    /// </summary>
    public class QueryProfiler : StorageListener
    {
        public class QueryInfo : IComparable
        {
            public String query;
            public long totalTime;
            public long maxTime;
            public long count;
            public bool sequentialSearch;

            public int CompareTo(object arg) 
            { 
                QueryInfo info = (QueryInfo)arg;
                return (totalTime > info.totalTime) ? -1 : (totalTime < info.totalTime) ? 1 
                    : (count > info.count) ? -1 : (count < info.count) ? 1: 0;
            } 
        }
        
        protected Hashtable profile = new Hashtable();
    
        public void QueryExecution(string query, long elapsedTime, bool sequentialSearch)  
        {
            lock (this)
            {
                QueryInfo info = (QueryInfo)profile[query];
                if (info == null) 
                {
                    info = new QueryInfo();
                    info.query = query;
                    profile[query] = info;
                }
                if (info.maxTime < elapsedTime) 
                { 
                    info.maxTime = elapsedTime;
                }
                info.totalTime += elapsedTime;
                info.count += 1;
                info.sequentialSearch |= sequentialSearch;
            }        
        }
#if !WINRT_NET_FRAMEWORK
        /// <summary>
        /// Dump queries execution profile to standard output
        /// </summary>
        public void Dump() 
        {
             Dump(Console.Out);
        }
#endif
        /// <summary>
        /// Dump queries execution profile to the specified destination
        /// </summary>
        /// <param name="writer">destination stream where profile should be dumped</param>
        public void Dump(TextWriter writer) 
        {
            QueryInfo[] results = GetProfile();
            writer.WriteLine("S     Total      Count Maximum Average Percent Query");
            long total = 0;
            foreach (QueryInfo info in results) 
            {
                total += info.totalTime;
            }
            string format = "{0}{1,10} {2,10} {3,7} {4,7} {5,6}% {6}"; 
            foreach (QueryInfo info in results) 
            {
                writer.WriteLine(format, 
                                 info.sequentialSearch ? '!' : ' ', 
                                 info.totalTime, 
                                 info.count,  
                                 info.maxTime, 
                                 (info.count != 0 ? info.totalTime/info.count : 0L), 
                                 (total != 0 ? info.totalTime*100/total : 0L), 
                                 info.query);
            }
        }

        /// <summary>
        /// Get array with QueryInfo elements sorted by (totalTime,count)
        /// </summary>
        public QueryInfo[] GetProfile() 
        {
            QueryInfo[] a = new QueryInfo[profile.Count];
            profile.Values.CopyTo(a, 0);
            Array.Sort(a);
            return a;
        }
    }
}