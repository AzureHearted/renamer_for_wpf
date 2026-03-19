using Microsoft.Xaml.Behaviors;
using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace ReNamer.Behaviors
{
    public class DataGridSelectedItemsBehavior : Behavior<DataGrid>
    {
        private bool _isUpdating;

        public IList SelectedItems
        {
            get => (IList)GetValue(SelectedItemsProperty);
            set => SetValue(SelectedItemsProperty, value);
        }

        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.Register(
                nameof(SelectedItems),
                typeof(IList),
                typeof(DataGridSelectedItemsBehavior),
                new PropertyMetadata(null, OnSelectedItemsChanged));

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.SelectionChanged += OnSelectionChanged;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.SelectionChanged -= OnSelectionChanged;
            base.OnDetaching();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdating || SelectedItems == null) return;

            _isUpdating = true;

            // DataGrid → VM 更新
            if (SelectedItems is INotifyCollectionChanged) // 只在 ObservableCollection 时尽量优化
            {
                foreach (var item in e.RemovedItems) SelectedItems.Remove(item);
                foreach (var item in e.AddedItems)
                {
                    // 小数据量检查重复，大数据量直接添加
                    if (AssociatedObject.SelectedItems.Count < 500)
                    {
                        if (!SelectedItems.Contains(item)) SelectedItems.Add(item);
                    }
                    else
                    {
                        SelectedItems.Add(item);
                    }
                }
            }
            else
            {
                // 如果不是 ObservableCollection，退回原逻辑
                SelectedItems.Clear();
                foreach (var item in AssociatedObject.SelectedItems)
                    SelectedItems.Add(item);
            }

            _isUpdating = false;
        }

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = (DataGridSelectedItemsBehavior)d;
            if (behavior.AssociatedObject == null) return;

            // 订阅 VM 集合变化
            if (e.NewValue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += (s, args) =>
                {
                    if (behavior._isUpdating) return;

                    behavior._isUpdating = true;
                    behavior.AssociatedObject.SelectionChanged -= behavior.OnSelectionChanged;

                    var dg = behavior.AssociatedObject;
                    switch (args.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            foreach (var item in args.NewItems)
                            {
                                if (dg.Items.Contains(item) && !dg.SelectedItems.Contains(item))
                                    dg.SelectedItems.Add(item);
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            foreach (var item in args.OldItems)
                                dg.SelectedItems.Remove(item);
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            dg.UnselectAll();
                            foreach (var item in behavior.SelectedItems)
                                if (dg.Items.Contains(item))
                                    dg.SelectedItems.Add(item);
                            break;
                    }

                    behavior.AssociatedObject.SelectionChanged += behavior.OnSelectionChanged;
                    behavior._isUpdating = false;
                };
            }
        }
    }
}