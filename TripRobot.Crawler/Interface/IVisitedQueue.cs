using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TripRobot.Crawler.Interface
{
    /// <summary>
    /// 已完成队列抽象
    /// </summary>
    public interface IVisitedQueue
    {
        /// <summary>
        /// URL 是否存在
        /// </summary>
        /// <returns></returns>
        bool Contains();

    }
}
