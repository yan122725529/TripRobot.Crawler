using System.Security.Cryptography.X509Certificates;

namespace TripRobot.Crawler.Interface
{
    /// <summary>
    /// 队列抽象
    /// </summary>
    public interface IQueue
    {
        void Add();
        void Remove();
        void GetEntity();
    }
}