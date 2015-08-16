namespace TripRobot.Crawler.Interface
{
    public interface IConfigGeter
    {
        #region 配置属性

        /// <summary>
        ///     缓存在内存中的DownLoaded 条数
        /// </summary>
        int CacheRowCount { get; set; }
        /// <summary>
        /// 线程总数
        /// </summary>
        int ThreadCount { get;  set; }

        #endregion

        /// <summary>
        ///     获取各种配置信息
        /// </summary>
        void GetConfig();
    }
}