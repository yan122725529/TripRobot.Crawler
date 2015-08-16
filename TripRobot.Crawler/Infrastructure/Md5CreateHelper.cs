using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TripRobot.Crawler.Infrastructure
{
    /// <summary>
    ///     MD5加密
    /// </summary>
    public static class Md5CreateHelper
    {
        public static string md5(string s)
        {
            var md5 = new MD5CryptoServiceProvider();
            var bytes = Encoding.UTF8.GetBytes(s);
            bytes = md5.ComputeHash(bytes);
            md5.Clear();

            var ret = bytes.Aggregate("", (current, t) => current + Convert.ToString(t, 16).PadLeft(2, '0'));

            return ret.PadLeft(32, '0');
        }
    }
}