using System;
using System.Collections.Generic;
using System.Threading;
using ServiceStack.Logging;
using TripRobot.Crawler.Etity;
using TripRobot.Crawler.Infrastructure;
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
            //获取各个参数
        }





        public void HandleQueue()
        {
            IVisitedQueue bloomVisitedQueue =new BloomVisitedQueue();
            ITodoQueue<UrlInfo> workQueue = new RedisWorkQueue();

            var info=workQueue.GetNext();
            //（当没有Visited and 不在已下载） 或者 在已下载但是有更新 （取10000条  做缓存过期）
            try
            {
                if (!bloomVisitedQueue.Contains(info.Key))
                {
                    Console.WriteLine("下载网页");
                    Console.WriteLine("分析网页");
                    Console.WriteLine("把超链接加入todo");
                    Console.WriteLine("把本连接加入visited");
                }
            }
            catch (Exception)
            {
                //日志
                //把弹出的info加入回work队列
                workQueue.Add(info);
            }
           


        }


      

        public void TestBoolm()
        {
            var a=new BloomVisitedQueue();
            a.Add("abc");
            a.Add("123");
            a.Add("342");
            a.Add("124");
            a.Add("1234");
            if (a.Contains("124"))
            {
                Console.WriteLine(  "124");
            }
            if (!a.Contains("jjj"))
            {
                Console.WriteLine( "jjj" );
            }
        }

        public void getinfo()
        {
            var a = new Thread(() =>
            {
                var WorkQueuea = new RedisWorkQueue();
                for (var i = 0; i < 100; i++)
                {

                    var info = WorkQueuea.GetNext();
                    if (info == null)
                    {
                        
                        Console.WriteLine("a======================================处理完啦");
                    }
                    else
                    {
                       
                       
                        Console.WriteLine("a-->" + info.Key + "-->" + info.UrlString);
                    }
                  

                    
                  
                }
                WorkQueuea.Dispose();
            });
            var b = new Thread(() =>
            {
                var WorkQueueb = new RedisWorkQueue();
                for (var i = 0; i < 100; i++)
                {

                    var info = WorkQueueb.GetNext();
                    if (info == null)
                    {

                        Console.WriteLine("b======================================处理完啦");
                    }
                    else
                    {
                        Console.WriteLine("a-->" + info.Key + "-->" + info.UrlString);
                      
                    }
                }
                WorkQueueb.Dispose();
            });

            var c = new Thread(() =>
            {
                var WorkQueuev = new RedisWorkQueue();
                for (var i = 0; i < 100; i++)
                {

                    var info = WorkQueuev.GetNext();
                    if (info == null)
                    {

                        Console.WriteLine("c======================================处理完啦");
                    }
                    else
                    {
                        
                     
                    }
                }
                WorkQueuev.Dispose();
            });
            a.Start();
            b.Start();
            c.Start();


        }

        public void testAdd()
        {
            var a = new Thread(() =>
            {
                var WorkQueuea = new RedisWorkQueue();
                for (var i = 0; i < 100; i++)
                {
                    var tmp = "aTherd" + Guid.NewGuid().ToString() + Guid.NewGuid().ToString() +
                              Guid.NewGuid().ToString() + Guid.NewGuid().ToString();
                    WorkQueuea.Add(
                        
                        new UrlInfo {UrlString = tmp
                       ,


                      Key  =Md5CreateHelper.md5(tmp) });
                        
                        
              
                    Console.WriteLine("a"+i.ToString());
                   
                   
                }
                WorkQueuea.Dispose();
            });


            var b = new Thread(() =>
            {
                var WorkQueueb = new RedisWorkQueue();
                for (var i = 0; i < 100; i++)
                {
                    var tmp = "bTherd" + Guid.NewGuid().ToString() + Guid.NewGuid().ToString() +
                              Guid.NewGuid().ToString() + Guid.NewGuid().ToString();
                    WorkQueueb.Add(

                        new UrlInfo
                        {
                            UrlString = tmp
                       ,


                            Key = Md5CreateHelper.md5(tmp)
                        });



                    Console.WriteLine("a" + i.ToString());
                }
                WorkQueueb.Dispose();
            });


            var c = new Thread(() =>
            {
                var WorkQueuec = new RedisWorkQueue();
                for (var i = 0; i < 100; i++)
                {
                    var tmp = "cTherd" + Guid.NewGuid().ToString() + Guid.NewGuid().ToString() +
                              Guid.NewGuid().ToString() + Guid.NewGuid().ToString();
                    WorkQueuec.Add(

                        new UrlInfo
                        {
                            UrlString = tmp
                       ,


                            Key = Md5CreateHelper.md5(tmp)
                        });



                    Console.WriteLine("a" + i.ToString());
                }
                WorkQueuec.Dispose();
            });


            var d = new Thread(() =>
            {
                var WorkQueued = new RedisWorkQueue();
                for (var i = 0; i < 100; i++)
                {
                    var tmp = "dTherd" + Guid.NewGuid().ToString() + Guid.NewGuid().ToString() +
                              Guid.NewGuid().ToString() + Guid.NewGuid().ToString();
                    WorkQueued.Add(

                        new UrlInfo
                        {
                            UrlString = tmp
                       ,


                            Key = Md5CreateHelper.md5(tmp)
                        });



                    Console.WriteLine("a" + i.ToString());
                }
                WorkQueued.Dispose();
            });


           

            a.Start();
            b.Start();
            c.Start();
            d.Start();
            
        }
    }
}