using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReNamer.Comparers
{
    public class CustomSortComparer : IComparer
    {
        private readonly string _property;
        private readonly ListSortDirection _direction;
        private readonly IComparer<string> _comparer;

        public CustomSortComparer(string property, ListSortDirection direction)
        {
            _property = property;
            _direction = direction;
            _comparer = WindowsExplorerComparer.Instance;
        }

        public int Compare(object x, object y)
        {
            var prop = TypeDescriptor.GetProperties(x)[_property];

            var valX = prop?.GetValue(x)?.ToString() ?? string.Empty;
            var valY = prop?.GetValue(y)?.ToString() ?? string.Empty;

            int result = _comparer.Compare(valX, valY);

            return _direction == ListSortDirection.Ascending ? result : -result;
        }
    }
}
