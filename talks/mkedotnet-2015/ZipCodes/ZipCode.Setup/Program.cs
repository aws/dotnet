using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ZipCode.DAL;

namespace ZipCode.Setup
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                ZipCodeManager.Instance.InitializeTable(@"..\..\..\US-ZipCodes.txt");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }

            Console.WriteLine("Done");
            Console.ReadLine();
        }
    }
}
