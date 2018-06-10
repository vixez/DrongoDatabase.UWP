using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database
{
    public class SqlHelper
    {
        public static string Escape(string input)
        {
            input = input.Replace("'", "''");

            return input;
        }
    }
}
