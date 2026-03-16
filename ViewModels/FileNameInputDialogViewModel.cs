using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReNamer.ViewModels
{
    public partial class FileNameInputDialogViewModel : ObservableObject
    {
        /// <summary>
        /// 文件名
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
        private string _name = String.Empty;

        /// <summary>
        /// 窗口标题
        /// </summary>
        [ObservableProperty]
        private string _title = "请输入文件名";

        /// <summary>
        /// 提示
        /// </summary>
        [ObservableProperty]
        private string _tips = "请输入名称：";


        /// <summary>
        /// 被禁用的名称
        /// </summary>
        // 在构造函数或声明处
        private HashSet<string> _disableNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);


        // 供 View 订阅的事件：true 表示确定关闭，false 或 null 表示取消
        public event Action<bool>? RequestClose;


        public FileNameInputDialogViewModel(string defaultName, string? title = null, string? tips = null, List<string>? disableNames = null)
        {
            _name = defaultName;
            if (title != null)
            {
                Title = title.Trim();
            }
            if (tips != null)
            {
                Tips = tips.Trim();
            }
            if (disableNames != null)
            {
                // 使用 UnionWith 将 List 中的内容批量添加到 HashSet 中
                // 它会自动应用我们在声明处定义的 OrdinalIgnoreCase 比较逻辑
                _disableNames.Clear();
                _disableNames.UnionWith(disableNames);
            }
        }

        private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        private bool CanConfirm()
        {
            if (string.IsNullOrWhiteSpace(Name)) return false;

            // 1. 剔除首尾空格（Windows 会自动忽略，但会导致校验不准）
            string trimmedName = Name.Trim();

            // 2. 检查禁用名单（确保 DisableNames 是 HashSet<string>）
            if (_disableNames.Contains(trimmedName)) return false;

            // 3. 检查非法字符
            if (trimmedName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) return false;

            // 4. 检查 Windows 保留名 (可选，看你需求硬核程度)
            // 比如文件名是 "CON.txt"，在 Windows 下也是非法的
            string nameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(trimmedName);
            if (WindowsReservedNames.Contains(nameWithoutExtension)) return false;

            return true;
        }

        [RelayCommand(CanExecute = nameof(CanConfirm))]
        private void Confirm()
        {
            // 业务逻辑执行完毕，请求关闭窗口并传递“成功”信号
            RequestClose?.Invoke(true);
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(false);
        }
    }
}
