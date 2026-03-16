using ReNamer.ViewModels;
using ReNamer.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ReNamer.Services
{
    public static class DialogService
    {
        /// <summary>
        /// 显示文件名输入确认弹窗
        /// </summary>
        /// <param name="defaultName">初始文件名</param>
        /// <param name="title">标题</param>
        /// <param name="tips">提示</param>
        /// <returns></returns>
        public static string? ShowFileNameInputDialogAsync(string defaultName, string? title = null, string? tips = null, List<string>? disableNames = null)
        {
            // 1. 创建 VM
            var vm = new FileNameInputDialogViewModel(defaultName, title, tips, disableNames);

            // 2. 创建 View
            var dialog = new FileNameInputDialog(vm)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            // 3. 这里的 ShowDialog 会阻塞，直到 dialog.DialogResult 被赋值并关闭
            // 注意：我们在 View 的后台代码里已经写了设置 DialogResult 的逻辑
            var result = dialog.ShowDialog();

            // 4. 判断结果。如果是 true，说明用户点了确定
            if (result == true)
            {
                return vm.Name; // 此时返回双向绑定后的新名字
            }

            return null; // 用户取消或直接点 X
        }
    }
}
