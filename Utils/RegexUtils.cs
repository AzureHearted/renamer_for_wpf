using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ReNamer.Utils
{
    public static class RegexUtils
    {
        /// <summary>
        /// 安全的创建正则表达式
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public static Regex? SafeCreateRegex(string? pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return null;

            try
            {
                return new Regex(pattern, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                // 如果正则表达式非法，返回一个永远不会匹配的 Regex
                return new Regex("$^");
            }
        }
    }
}
