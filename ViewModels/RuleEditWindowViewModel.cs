using ReNamer.Models.ReName;
using ReNamer.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ReNamer.ViewModels
{
    public partial class RuleEditWindowViewModel : ObservableObject
    {
        // 规则池
        public ObservableCollection<BaseRule> RulePool = new();

        /// <summary>
        /// 当前索引
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentRule))]
        private int _currentTabIndex;

        /// <summary>
        /// 当前规则
        /// </summary>
        public BaseRule CurrentRule => RulePool[CurrentTabIndex];

        partial void OnCurrentTabIndexChanged(int oldValue, int newValue)
        {

            var newRule = RulePool[newValue];
            if (newRule != null)
            {
                newRule.IsSilenced = false;
            }

            var oldRule = RulePool[oldValue];
            if (oldRule != null)
            {
                oldRule.IsSilenced = true;
            }
        }

        // 模式枚举
        public enum WindowMode
        {
            [Description("添加")]
            Add,
            [Description("编辑")]
            Edit
        }

        // 当前窗口模式
        [ObservableProperty]
        private WindowMode _mode;

        // 窗口标题
        public string WinTile => $"{Utils.EnumExtensions.GetDescription(Mode)}规则";

        public event Action<BaseRule>? OnAddRule;
        public event Action<BaseRule>? OnSaveRule;
        public event Action? OnCancel;

        public RuleEditWindowViewModel(BaseRule? rule)
        {

            RulePool.Add(new InsertRule());
            RulePool.Add(new ReplaceRule());
            RulePool.Add(new RemoveRule());
            RulePool.Add(new SerializeRule());
            RulePool.Add(new FillRule());
            RulePool.Add(new RegexRule());
            RulePool.Add(new ExtensionRule());


            if (rule != null)
            {
                Mode = WindowMode.Edit;

                var cloned = rule.Clone();

                if (cloned != null)
                {
                    // 3. 重点：找到池子里类型一致的那个，把它换掉
                    // 这样无论 rule.Type 的整数值是多少，逻辑永远正确
                    var target = RulePool.FirstOrDefault(r => r.Type == cloned.Type);
                    if (target != null)
                    {
                        int index = RulePool.IndexOf(target);
                        RulePool[index] = cloned; // 替换引用，触发 UI 更新
                        CurrentTabIndex = index;     // 让 Tab 页切到对应的规则
                    }
                }
            }
            else
            {

                Mode = WindowMode.Add;
                CurrentTabIndex = (int)RuleType.Insert;
                Debug.WriteLine($"新增模式：${CurrentTabIndex}");
                CurrentRule.IsSilenced = false;
            }
        }

        [RelayCommand]
        private void AddRule()
        {
            OnAddRule?.Invoke(CurrentRule);
        }

        [RelayCommand]
        private void SaveRule()
        {
            Debug.WriteLine("保存规则");
            OnSaveRule?.Invoke(CurrentRule);
        }

        [RelayCommand]
        private void Cancel()
        {
            OnCancel?.Invoke();
        }


    }
}
