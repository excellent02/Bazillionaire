using bazillionaire;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord_Bot
{
    class Program
    {
        static void Main(string[] args)
        {
            BazillionBot aaa = new BazillionBot();
            aaa.RunAsync().GetAwaiter().GetResult();
        }
    }
}
