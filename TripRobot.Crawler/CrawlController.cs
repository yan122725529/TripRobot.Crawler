using System;
using System.Linq;
using System.Net;
using System.Threading;
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
        public CrawlController()
        {
            Initialize();
        }

        /// <summary>
        ///     初始化
        /// </summary>
        private void Initialize()
        {
            //实例化布隆筛选器
            BloomVisitedQueue = new BloomVisitedQueue();
            //todo 获取config
            Config();
            InitSeedsUrl();
            InitThreads();
        }

        protected void Config()
        {
            ThreadsCount = 4;
            ConnectMax = 20;
        }

        //初始化线程相关属性
        protected void InitThreads()
        {
            threads = new Thread[ThreadsCount];
            threadStatus = new bool[ThreadsCount];
            //设置最大连接数
            ServicePointManager.DefaultConnectionLimit = ConnectMax;

            for (var i = 0; i < threads.Length; i++)
            {
                var threadStart = new ParameterizedThreadStart(CrawlProcess);

                threads[i] = new Thread(threadStart);
            }
        }

        /// <summary>
        ///     向队列中加入种子URL
        /// </summary>
        protected void InitSeedsUrl()
        {
            var WorkQueuea = new RedisWorkQueue();
            for (var i = 0; i < 1000; i++)
            {
                var tmp = "" + Guid.NewGuid() + Guid.NewGuid() +
                          Guid.NewGuid() + Guid.NewGuid();
                WorkQueuea.Add(
                    new UrlInfo
                    {
                        UrlString = tmp
                        ,
                        Key = Md5CreateHelper.md5(tmp)
                    });


                Console.WriteLine("a" + tmp);
            }
            WorkQueuea.Dispose();
        }

        /// <summary>
        ///     开始所有线程
        /// </summary>
        public void Start()
        {
            for (var i = 0; i < threads.Length; i++)
            {
                threads[i].IsBackground = true;
                threads[i].Start(i);

                threadStatus[i] = false;
            }

            //保证每个线程依次打开
            //while (newThread.Any(tt => tt.IsAlive))
            //{
            //    Thread.Sleep(50);
            //}
        }

        /// <summary>
        ///     关闭所有线程
        /// </summary>
        public void Stop()
        {
            foreach (var thread in threads)
            {
                thread.Abort();
            }
        }

        /// <summary>
        ///     The config request.
        /// </summary>
        /// <param name="request">
        ///     The request.
        /// </param>
        private void ConfigRequest(HttpWebRequest request)
        {
            //request.UserAgent = this.Settings.UserAgent;
            request.CookieContainer = cookieContainer;
            request.AllowAutoRedirect = true;
            request.MediaType = "text/html";
            request.Headers["Accept-Language"] = "zh-CN,zh;q=0.8";


            request.Timeout = Timeout;
        }

        /// <summary>
        ///     线程执行方法
        /// </summary>
        /// <param name="threadIndex"></param>
        protected void CrawlProcess(object threadIndex)
        {
            var currentThreadIndex = (int) threadIndex;

            //Redis工作队列
            ITodoQueue<UrlInfo> workQueue = new RedisWorkQueue();


            while (true)
            {
                var info = workQueue.GetNext();


                // 根据队列中取出数据是否为null & 空闲线程的数量，判断线程是睡眠还是退出
                if (info == null)
                {
                    threadStatus[currentThreadIndex] = true;
                    if (!threadStatus.Any(t => t == false))
                    {
                        Console.WriteLine(currentThreadIndex + "处理完啦");
                        break;
                    }

                    Thread.Sleep(2000);
                    continue;
                }

                threadStatus[currentThreadIndex] = false;


                HttpWebRequest request = null;
                HttpWebResponse response = null;


                try
                {
                    if (!BloomVisitedQueue.Contains(info.Key))
                    {
                        //todo 检查是否在数据库且有更新
                        request = WebRequest.Create(info.UrlString) as HttpWebRequest;
                        ConfigRequest(request);

                        Console.WriteLine("分析网页");
                        Console.WriteLine("把超链接加入todo");
                        BloomVisitedQueue.Add(info.Key);
                        Console.WriteLine("把本连接加入visited");
                    }
                    else
                    {
                        Console.WriteLine(info.Key + "...完成过了");
                    }
                }
                catch (Exception)
                {
                    //日志
                    //把弹出的info加入回work队列
                    workQueue.Add(info);
                }


                //try
                //{


                //    //// 1~5 秒随机间隔的自动限速
                //    //if (this.Settings.AutoSpeedLimit)
                //    //{
                //    //    int span = this.random.Next(1000, 5000);
                //    //    Thread.Sleep(span);
                //    //}

                //    // 创建并配置Web请求
                //    request = WebRequest.Create(urlInfo.UrlString) as HttpWebRequest;
                //    this.ConfigRequest(request);

                //    if (request != null)
                //    {
                //        response = request.GetResponse() as HttpWebResponse;
                //    }

                //    if (response != null)
                //    {
                //        this.PersistenceCookie(response);

                //        Stream stream = null;

                //        // 如果页面压缩，则解压数据流
                //        if (response.ContentEncoding == "gzip")
                //        {
                //            var responseStream = response.GetResponseStream();
                //            if (responseStream != null)
                //            {
                //                stream = new GZipStream(responseStream, CompressionMode.Decompress);
                //            }
                //        }
                //        else
                //        {
                //            stream = response.GetResponseStream();
                //        }

                //        using (stream)
                //        {
                //            string html = this.ParseContent(stream, response.CharacterSet);

                //            this.ParseLinks(urlInfo, html);

                //            if (this.DataReceivedEvent != null)
                //            {
                //                this.DataReceivedEvent(
                //                    new DataReceivedEventArgs
                //                    {
                //                        Url = urlInfo.UrlString,
                //                        Depth = urlInfo.Depth,
                //                        Html = html
                //                    });
                //            }

                //            if (stream != null)
                //            {
                //                stream.Close();
                //            }
                //        }
                //    }
                //}
                //catch (Exception exception)
                //{
                //    if (this.CrawlErrorEvent != null)
                //    {
                //        if (urlInfo != null)
                //        {
                //            this.CrawlErrorEvent(
                //                new CrawlErrorEventArgs {Url = urlInfo.UrlString, Exception = exception});
                //        }
                //    }
                //}
                //finally
                //{
                //    if (request != null)
                //    {
                //        request.Abort();
                //    }

                //    if (response != null)
                //    {
                //        response.Close();
                //    }
                //}
            }
        }

        #region 属性

        /// <summary>
        ///     最大链接数，从配置读取
        /// </summary>
        private int ConnectMax;

        /// <summary>
        ///     线程的对象的数组
        /// </summary>
        private Thread[] threads;

        /// <summary>
        ///     线程的个数，从配置读取
        /// </summary>
        private int ThreadsCount;

        /// <summary>
        ///     线程的状态（是否有任务在当中执行）数组
        /// </summary>
        private bool[] threadStatus;


        private int Timeout;


        public IVisitedQueue BloomVisitedQueue { get; private set; }


        /// <summary>
        ///     The cookie container.
        /// </summary>
        private readonly CookieContainer cookieContainer;

        #endregion
    }
}