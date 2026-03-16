using ReNamer.ViewModels;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace ReNamer.Views
{
    /// <summary>
    /// FileNameInputDialog.xaml 的交互逻辑
    /// </summary>
    public partial class FileNameInputDialog
    {
        public FileNameInputDialog(FileNameInputDialogViewModel vm)
        {
            InitializeComponent();

            this.DataContext = vm; // 绑定 VM

            // 核心：订阅 VM 的关闭信号。
            // 这不违反 MVVM，因为 View 知道如何关闭自己，而信号是 VM 发出的。
            vm.RequestClose += (success) =>
            {
                // 检查当前窗口是否以模态方式显示
                // ComponentDispatcher.IsThreadModal 是一种判断方式，
                // 但最直接的方法是捕获异常或判断窗口状态
                try
                {
                    this.DialogResult = success;
                }
                catch (InvalidOperationException)
                {
                    // 如果不是以 ShowDialog 方式打开，DialogResult 不可用
                    // 此时直接关闭即可，调用方通过其他方式获取结果
                    this.Close();
                }
            };
        }
    }
}
