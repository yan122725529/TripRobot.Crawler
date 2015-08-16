using System.Collections.Generic;
using TripRobot.Crawler.Etity;

namespace TripRobot.Crawler.Interface
{
    /// <summary>
    ///     Todo队列抽象
    /// </summary>
    public interface ITodoQueue : IQueue
    {
        /// <summary>
        ///     获取种子Url
        /// </summary>
        /// <returns></returns>
        IList<UrlInfo> GetSeedUrl();

        /// <summary>
        /// 获得下一个Url
        /// </summary>
        void MoveNext();
    }
}