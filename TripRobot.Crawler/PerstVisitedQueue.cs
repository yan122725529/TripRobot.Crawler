using Perst;
using TripRobot.Crawler.Interface;

namespace TripRobot.Crawler
{
    public class PerstVisitedQueue: IVisitedQueue
    {
        public PerstVisitedQueue()
        {
        }


        #region 配置内存数据库

        private void configDb()
        {
            var db = StorageFactory.Instance.CreateStorage();
            int pagePoolSize = 32*1024*1024;
            db.Open("UrlVisited.dbs", pagePoolSize);

        }

        #endregion







        public void Add()
        {
            throw new System.NotImplementedException();
        }

        public void Remove()
        {
            throw new System.NotImplementedException();
        }

        public void GetEntity()
        {
            throw new System.NotImplementedException();
        }

        public bool Contains()
        {
            throw new System.NotImplementedException();
        }
    }
}