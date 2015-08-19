using TripRobot.Crawler.Etity;
using TripRobot.Crawler.Infrastructure;
using TripRobot.Crawler.Interface;

namespace TripRobot.Crawler
{
    public class BloomVisitedQueue: IVisitedQueue
    {
        public BloomVisitedQueue()
        {
            var filter=new BloomFilter<UrlInfo>(100000);
        }

        public bool Contains()
        {
            
        }

        public void Add()
        {
            throw new System.NotImplementedException();
        }
    }
}