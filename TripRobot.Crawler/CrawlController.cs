using System;
using TripRobot.Crawler.Etity;
using TripRobot.Crawler.Interface;

namespace TripRobot.Crawler
{
    /// <summary>
    ///     爬虫控制器
    /// </summary>
    public class CrawlController
    {
        public ITodoQueue<UrlInfo> WorkQueue;
        public CrawlController()
        {
            WorkQueue=new PeestWorkQueue();
        }

        public void Run()
        {

            foreach (var url in WorkQueue.GetSeedUrl())
            {
                WorkQueue.Add(url);
                var a = WorkQueue.GetNext();
                Console.WriteLine(a.UrlMd5key + a.UrlString);    
            }

            
        }


    }
}