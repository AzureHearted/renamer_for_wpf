using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ReNamer.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        // 从 Enum 传向 View (IsChecked)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return value.ToString() == parameter.ToString();
        }

        // 从 View (IsChecked) 传回 Enum
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 组合检查：
            // - value 是否为 true (只有勾选的 RadioButton 才触发更新)
            // - parameter 是否为 string (通过 is string 自动处理了 null 检查)
            if (value is bool isChecked && isChecked && parameter is string enumString)
            {
                try
                {
                    return Enum.Parse(targetType, enumString);
                }
                catch (Exception)
                {
                    // 如果解析失败（比如参数填错），返回 DoNothing 而不是崩溃
                    return Binding.DoNothing;
                }
            }

            // 如果没勾选，或者参数不对，直接返回 DoNothing 保持原值
            return Binding.DoNothing;
        }
    }
}
