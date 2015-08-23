using System;
using System.Collections.Generic;
using TripRobot.Crawler.Etity;
using TripRobot.Crawler.Interface;
using TripRobot.Crawler.Redis;

namespace TripRobot.Crawler
{
    public class RedisWorkQueue : ITodoQueue<UrlInfo>, IDisposable
    {
       
        private RedisRepository<UrlInfo> redisRepository;


        public RedisWorkQueue()
        {
            redisRepository=new RedisRepository<UrlInfo>();
        }


        public UrlInfo GetNext()
        {
            return redisRepository.MoveNext();
        }

        

        public void Add(UrlInfo info )
        {
            redisRepository.Insert(info);
        }

        public void Remove(UrlInfo info)
        {
            redisRepository.Delete(info);
        }

        public void Dispose()
        {
            redisRepository.Dispose();
        }
    }
}