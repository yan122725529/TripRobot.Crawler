using System;
using System.Collections.Generic;
using Perst;
using TripRobot.Crawler.Etity;
using TripRobot.Crawler.Infrastructure;
using TripRobot.Crawler.Interface;

namespace TripRobot.Crawler
{
    public class PeestWorkQueue : ITodoQueue<UrlInfo>, IDisposable
    {
        public void Dispose()
        {
            StorageDB.Storage.Close(); //关闭数据连接
            StorageDB = null;
        }

        public IList<string> GetSeedUrl()
        {
            return new List<string> {@"http://www.ly.com/zizhuyou/"};
        }

        public UrlInfo GetNext()
        {
            var list = StorageDB.GetRecords(typeof (UrlInfo)).GetEnumerator();
            if (list.MoveNext())
            {
                return (UrlInfo) list.Current;
            }
            return null;
        }

        public void Add(string urlString)
        {
            var urlInfo = new UrlInfo
            {
                UrlMd5key = Md5CreateHelper.md5(urlString),
                UrlString = urlString
            };
            StorageDB.AddRecord(typeof (UrlInfo), urlInfo); //插入操作 参数：插入表的类型，插入对象

            StorageDB.Storage.Commit();
        }

        public void Remove(string urlString)
        {
            var urlInfo = new UrlInfo
            {
                UrlMd5key = Md5CreateHelper.md5(urlString),
                UrlString = urlString
            };


            StorageDB.DeleteRecord(typeof (UrlInfo), urlInfo); //删除操作 参数：删除表的类型，删除对象

            StorageDB.Storage.Commit();
        }

        public UrlInfo GetEntity(string Md5Key)
        {
            var re = new UrlInfo();
            foreach (var info in StorageDB.Select(typeof (UrlInfo), string.Format("UrlMd5key ={0}", Md5Key)))

            {
                re = (UrlInfo) info;
            }
            return re;
        }

        private Database OpenDb()
        {
            var storage = StorageFactory.Instance.CreateStorage();
            storage.SetProperty("perst.file.extension.quantum", 512*1024); //初始化存储大小为512MB

            storage.SetProperty("perst.extension.quantum", 256*1024); //每次递增的存储大小为256


            storage.Open("TodoDB.dbs");
            var db = new Database(storage); //创建操作数据库的对象

            db.CreateTable(typeof (UrlInfo)); //创建class类型的表,成功返回TRUE,如果存在返回FALSE;
            db.CreateIndex(typeof (UrlInfo), "UrlMd5key", false); //创建索引参数：类型，对应字段，是否唯一
            _storageDB = db;
            return db;
        }

        #region 字段

        private Database _storageDB;

        /// <summary>
        ///     内存数据库
        /// </summary>
        public Database StorageDB
        {
            private set { _storageDB = value; }
            get { return _storageDB ?? (_storageDB = OpenDb()); }
        }

        #endregion
    }
}