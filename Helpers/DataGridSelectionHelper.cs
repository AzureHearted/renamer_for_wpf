using System;
using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace ReNamer.Helpers
{
    /// <summary>
    /// DataGrid 选择助手
    /// </summary>
    public static class DataGridSelectionHelper
    {
        // 标记位：防止 UI -> VM -> UI 的死循环响应
        private static bool _isUpdating;

        /// <summary>
        /// 注册 SelectedItems 属性
        /// </summary>
        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.RegisterAttached("SelectedItems", typeof(IList), typeof(DataGridSelectionHelper),
                new FrameworkPropertyMetadata(null, OnSelectedItemsChanged));

        public static void SetSelectedItems(DependencyObject element, IList value) => element.SetValue(SelectedItemsProperty, value);
        public static IList GetSelectedItems(DependencyObject element) => (IList)element.GetValue(SelectedItemsProperty);

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dg)
            {
                // 1. 订阅 UI 变化事件
                dg.SelectionChanged -= DataGrid_SelectionChanged;
                dg.SelectionChanged += DataGrid_SelectionChanged;

                // 2. 订阅 VM 集合变化
                if (e.NewValue is INotifyCollectionChanged observableList)
                {
                    // 先移除旧的事件防止内存泄漏
                    if (e.OldValue is INotifyCollectionChanged oldList)
                        observableList.CollectionChanged -= (s, args) => { };

                    observableList.CollectionChanged += (s, args) =>
                    {
                        if (_isUpdating) return; // 如果是 UI 引起的变更，VM 集合不用反向控制 UI

                        _isUpdating = true;
                        dg.SelectionChanged -= DataGrid_SelectionChanged;

                        try
                        {
                            switch (args.Action)
                            {
                                case NotifyCollectionChangedAction.Add:
                                    if (args.NewItems != null)
                                    {
                                        foreach (var item in args.NewItems)
                                        {
                                            // 只有当 DataGrid 确实包含这个对象引用时才添加
                                            if (dg.Items.Contains(item))
                                            {
                                                if (!dg.SelectedItems.Contains(item))
                                                {
                                                    dg.SelectedItems.Add(item);
                                                }
                                            }
                                            else
                                            {
                                                // 调试用：如果进到这里，说明你 VM 里的对象和 UI 里的对象不是同一个引用
                                                System.Diagnostics.Debug.WriteLine("警告：选中的对象不在 DataGrid 内容列表中");
                                            }
                                        }
                                    }
                                    break;

                                case NotifyCollectionChangedAction.Remove:
                                    foreach (var item in args.OldItems) dg.SelectedItems.Remove(item);
                                    break;

                                case NotifyCollectionChangedAction.Reset:
                                    dg.UnselectAll();
                                    // 关键：如果是 Reset（比如 SelectedFiles.Clear() 后重新 AddRange）
                                    // 此时 args.NewItems 可能是空的，需要从数据源重新同步
                                    if (GetSelectedItems(dg) is IList vmList)
                                    {
                                        foreach (var item in vmList)
                                        {
                                            if (dg.Items.Contains(item)) dg.SelectedItems.Add(item);
                                        }
                                    }
                                    break;
                            }
                        }
                        finally
                        {
                            dg.SelectionChanged += DataGrid_SelectionChanged;
                            _isUpdating = false;
                        }
                    };
                }
            }
        }

        private static void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdating) return;

            var dg = (DataGrid)sender;
            var list = GetSelectedItems(dg);
            if (list == null) return;

            _isUpdating = true;
            try
            {
                // 这里的 list.Remove 和 list.Add 依然存在 2万次通知的问题
                // 如果 VM 的集合是 ObservableCollection，建议批量操作
                foreach (var item in e.RemovedItems) list.Remove(item);
                foreach (var item in e.AddedItems)
                {
                    // Contains 是 O(n) 操作，2万次就是 O(n^2)，大数据量必死
                    // 只有在数据量小时才检查
                    if (dg.SelectedItems.Count < 500)
                    {
                        if (!list.Contains(item)) list.Add(item);
                    }
                    else
                    {
                        list.Add(item);
                    }
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }
}