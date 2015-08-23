using System.Collections.Generic;
using TripRobot.Crawler.Etity;

namespace TripRobot.Crawler.Interface
{
    /// <summary>
    ///     Todo队列抽象
    /// </summary>
    public interface  ITodoQueue<T> where T:new ()
    {
       
        /// <summary>
        /// 获得下一个Url
        /// </summary>
        T GetNext();

        void Add(T info);
        void Remove(T info);
     
   
    }
}