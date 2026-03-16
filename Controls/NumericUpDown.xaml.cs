using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ReNamer.Controls
{
    /// <summary>
    /// NumericUpDown.xaml 的交互逻辑
    /// </summary>
    public partial class NumericUpDown : UserControl
    {
        #region Dependency Properties

        // Value 属性：增加 CoerceValueCallback 确保数值安全
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(int), typeof(NumericUpDown),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceValue));

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        // Max 属性
        public static readonly DependencyProperty MaxProperty =
            DependencyProperty.Register(nameof(Max), typeof(int), typeof(NumericUpDown), new PropertyMetadata(int.MaxValue));

        public int Max
        {
            get => (int)GetValue(MaxProperty);
            set => SetValue(MaxProperty, value);
        }

        // Min 属性
        public static readonly DependencyProperty MinProperty =
            DependencyProperty.Register(nameof(Min), typeof(int), typeof(NumericUpDown), new PropertyMetadata(int.MinValue));

        public int Min
        {
            get => (int)GetValue(MinProperty);
            set => SetValue(MinProperty, value);
        }

        #endregion

        public NumericUpDown()
        {
            InitializeComponent();
        }

        #region Logic Handlers

        private void Increase_Click(object sender, RoutedEventArgs e) => Value++;

        private void Decrease_Click(object sender, RoutedEventArgs e) => Value--;

        private void txtValue_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) Value++;
            else Value--;
            e.Handled = true; // 拦截滚轮，防止外层 ScrollViewer 跟着滚动
        }

        #endregion

        #region Value Coercion

        // 这是一个“过滤器”，在值真正改变前进行强制修正
        private static object CoerceValue(DependencyObject d, object baseValue)
        {
            var control = (NumericUpDown)d;
            int value = (int)baseValue;

            if (value < control.Min) return control.Min;
            if (value > control.Max) return control.Max;
            return value;
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 如果需要值改变时触发特定事件，可以在这里写
        }

        #endregion
    }
}
