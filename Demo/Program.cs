using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TripRobot.Crawler;

namespace Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            var a=new CrawlController();
            a.Run();
            Console.ReadKey();
        }
    }
}
