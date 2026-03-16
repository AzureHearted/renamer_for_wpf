using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace ReNamer.Converters
{
    /// <summary>
    /// 枚举转换为 Visibility 值的转换器
    /// </summary>
    public class EnumToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 转换
        /// </summary>
        /// <param name="value">原始数据</param>
        /// <param name="targetType">Visibility 的类型</param>
        /// <param name="parameter">ConverterParameter 传递的值</param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            string? currentState = value.ToString();
            string? targetState = parameter.ToString();

            if(currentState == null || targetState == null)
            {
                return Visibility.Collapsed;
            }

            // 如果当前模式与参数指定的模式一致，则显示
            if (currentState.Equals(targetState, StringComparison.InvariantCultureIgnoreCase))
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
