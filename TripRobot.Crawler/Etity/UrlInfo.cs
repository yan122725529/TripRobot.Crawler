using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Perst;
using TripRobot.Crawler.Infrastructure;

namespace TripRobot.Crawler.Etity
{
    public class UrlInfo : Persistent
    {
        public string UrlString { get; set; }

        public string UrlMd5key { get; set; }
        //public string ConvertToMd5()
        //{
        //    return Md5CreateHelper.md5(UrlString);
        //}
    }

    public class UrlInfoRoot : Persistent
    {
        internal Index keyInex;
        internal FieldIndex urlIndex;
    }
}
