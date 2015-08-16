using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TripRobot.Crawler.Infrastructure;

namespace TripRobot.Crawler.Etity
{
    public class UrlInfo
    {
        public string UrlString { get; set; }

        
        public string ConvertToMd5()
        {
            return Md5CreateHelper.md5(UrlString);
        }
    }
}
