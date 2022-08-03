using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CY
{
    /// <summary>
    /// Useless for now
    /// </summary>
    public class GenericCompare : IEqualityComparer<string>
    {
        
        public bool Equals(string x, string y)
        {
            Match m1 = Regex.Match(x as string, @"https?:\/\/[^\/]+(\/.+\.html)");
            Match m2 = Regex.Match(y as string, @"https?:\/\/[^\/]+(\/.+\.html)");
            if (m1.Groups[1].Value == m2.Groups[1].Value)
                return true;
            return false;
        }
        public int GetHashCode(string obj)
        {
            //return obj.GetHashCode();
            return 1;
        }
    }
}
