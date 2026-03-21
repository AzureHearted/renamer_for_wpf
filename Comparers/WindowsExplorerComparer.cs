using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ReNamer.Comparers
{
    /// <summary>
    /// 类似Windows资源管理器的自然排序器
    /// </summary>
    public class WindowsExplorerComparer : IComparer<string>
    {
        public static readonly WindowsExplorerComparer Instance = new();

        private WindowsExplorerComparer() { }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string x, string y);

        public int Compare(string x, string y)
        {
            x ??= string.Empty;
            y ??= string.Empty;
            return StrCmpLogicalW(x, y);
        }
    }

}
