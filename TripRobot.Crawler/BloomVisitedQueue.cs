using TripRobot.Crawler.Etity;
using TripRobot.Crawler.Infrastructure;
using TripRobot.Crawler.Interface;

namespace TripRobot.Crawler
{
    public class BloomVisitedQueue: IVisitedQueue
    {
        private BloomFilter<string> Filter;
        public BloomVisitedQueue()
        {
             Filter= new BloomFilter<string>(200000);
        }


        public bool Contains(string key)
        {
           return Filter.Contains(key);
        }

        public void Add(string key)
        {
            Filter.Add(key);
        }
    }
}