using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GongSolutions.Wpf.DragDrop;
using HandyControl.Controls;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using ReNamer.Engines;
using ReNamer.Models.ReName;
using ReNamer.Services;
using ReNamer.Utils;
using ReNamer.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using static ReNamer.ViewModels.MainWindowViewModel;

namespace ReNamer.ViewModels
{

    /// <summary>
    /// 视图模型，继承了上面的 INotifyPropertyChanged 实现的一个类
    /// </summary>
    //public class MainWindowViewModel : INotify

    // 必须标记为 partial，因为编译器要帮写另一半代码
    public partial class MainWindowViewModel : ObservableObject, IDropTarget
    {

        // 文件列表
        [ObservableProperty]
        private ObservableCollection<ReNameFile> _files = [];


        partial void OnFilesChanged(ObservableCollection<ReNameFile>? oldValue, ObservableCollection<ReNameFile> newValue)
        {
            // --- 1. 清理旧集合的监听 (防内存泄漏) ---
            if (oldValue != null)
            {
                oldValue.CollectionChanged -= OnFilesCollectionChanged;

                // 获取旧视图并清理 PropertyChanged
                var oldView = CollectionViewSource.GetDefaultView(oldValue);
                if (oldView is INotifyPropertyChanged npc)
                    npc.PropertyChanged -= OnViewFilesPropertyChanged;
            }

            // --- 2. 挂载新集合的监听 ---
            if (newValue != null)
            {
                // 监听数据层：Add, Remove, Reset (处理 CanUserDeleteRows)
                newValue.CollectionChanged += OnFilesCollectionChanged;

                // 获取新视图
                var viewRules = CollectionViewSource.GetDefaultView(newValue);

                // 监听排序层：点击表头 SortDescriptions 变化
                if (viewRules is INotifyPropertyChanged npc)
                {
                    npc.PropertyChanged += OnViewFilesPropertyChanged;
                }
            }

            NotifyCommands();
        }

        // 处理集合变动

        private async void OnFilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            await RefreshAll();
        }

        // 处理排序变动
        private async void OnViewFilesPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            await RefreshAll();
        }

        // 规则列表
        [ObservableProperty]

        private ObservableCollection<BaseRule> _rules = [];


        partial void OnRulesChanged(ObservableCollection<BaseRule>? oldValue, ObservableCollection<BaseRule> newValue)
        {
            // --- 1. 清理旧集合的监听 (防内存泄漏) ---
            if (oldValue != null)
            {
                oldValue.CollectionChanged -= OnRulesCollectionChanged;

                // 获取旧视图并清理 PropertyChanged
                var oldView = CollectionViewSource.GetDefaultView(oldValue);
                if (oldView is INotifyPropertyChanged npc)
                    npc.PropertyChanged -= OnViewRulesPropertyChanged;
            }

            // --- 2. 挂载新集合的监听 ---
            if (newValue != null)
            {
                // 监听数据层：Add, Remove, Reset (处理 CanUserDeleteRows)
                newValue.CollectionChanged += OnRulesCollectionChanged;

                // 获取新视图
                var viewRules = CollectionViewSource.GetDefaultView(newValue);

                // 监听排序层：点击表头 SortDescriptions 变化
                if (viewRules is INotifyPropertyChanged npc)
                {
                    npc.PropertyChanged += OnViewRulesPropertyChanged;
                }
            }

            NotifyCommands();
        }

        // 处理集合变动
        private async void OnRulesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            await RefreshAll();
        }

        // 处理排序变动
        private async void OnViewRulesPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            await RefreshAll();
        }



        // 预设目录
        [ObservableProperty]
        private string _presetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Presets");

        // 预设列表
        [ObservableProperty]
        private ObservableCollection<Preset> _presets = [];


        // 当前预设索引
        [ObservableProperty]
        private Preset _currentPreset = new() { Name = "新预设" };

        partial void OnCurrentPresetChanged(Preset? oldValue, Preset newValue)
        {
            if (newValue != null)
            {
                Rules.Clear();
                Rules = [.. newValue.Rules.Select(x => x.Clone())];
                _ = RefreshAll();
            }
        }


        /// <summary>
        /// 当前 DataGrid 中视觉上的文件集合（包含排序、过滤后的顺序）
        /// </summary>
        public IEnumerable<ReNameFile> SortedFiles
        {
            get
            {
                // GetDefaultView 会返回绑定到 Files 的那个 ICollectionView 实例
                var view = CollectionViewSource.GetDefaultView(Files);

                // 将视图中的内容转为 IEnumerable<ReNameFile>
                // 这里的顺序就是 DataGrid 此时此刻显示的顺序
                return view.Cast<ReNameFile>();
            }
        }

        /// <summary>
        /// 当前 DataGrid 中视觉上的规则集合（包含排序、过滤后的顺序）
        /// </summary>
        public IEnumerable<BaseRule> SortedRules
        {
            get
            {
                // GetDefaultView 会返回绑定到 Rules 的那个 ICollectionView 实例
                var view = CollectionViewSource.GetDefaultView(Rules);

                // 将视图中的内容转为 IEnumerable<BaseRule>
                // 这里的顺序就是 DataGrid 此时此刻显示的顺序
                return view.Cast<BaseRule>();
            }
        }

        // 选中的 Rules
        [ObservableProperty]
        private ObservableCollection<BaseRule> _selectedRules = [];

        partial void OnSelectedRulesChanged(ObservableCollection<BaseRule>? oldValue, ObservableCollection<BaseRule> newValue)
        {
            NotifyCommands();
        }

        // 选中的 Files
        [ObservableProperty]
        private ObservableCollection<ReNameFile> _selectedFiles = [];

        partial void OnSelectedFilesChanged(ObservableCollection<ReNameFile>? oldValue, ObservableCollection<ReNameFile> newValue)
        {
            NotifyCommands();
        }

        /// <summary>
        /// 状态栏信息
        /// </summary>
        [ObservableProperty]
        private string _statusMessage = String.Empty;


        /// <summary>
        /// 是否有事情正在处理
        /// </summary>
        [ObservableProperty]
        private bool _processing;

        /// <summary>
        /// 显示进度条
        /// </summary>
        [ObservableProperty]
        private bool _isProgressVisible = false;

        /// <summary>
        /// 进度条值
        /// </summary>
        [ObservableProperty]
        private int _progressValue = 0;

        /// <summary>
        /// 添加规则事件
        /// </summary>
        [RelayCommand]
        private void AddRule()
        {
            OpenRuleEditor();
        }

        /// <summary>
        /// 打开规则编辑窗口
        /// </summary>
        /// <param name="rule">要编辑的规则</param>
        private void OpenRuleEditor(BaseRule? rule = null)
        {
            // 创建ViewModel
            var editorViewModel = new RuleEditWindowViewModel(rule);

            // 创建View
            var editorWindow = new RuleEditWindow(editorViewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };



            if (rule != null)
            {
                editorViewModel.OnSaveRule += async (newRule) =>
                {
                    int index = Rules.IndexOf(rule);
                    if (index >= 0)
                    {
                        Rules[index] = newRule;

                        // 成功修改一个规则后进行一次计算
                        await RefreshAll();
                    }
                };
            }
            else
            {
                editorViewModel.OnAddRule += async (newRule) =>
                {
                    Rules.Add(newRule);

                    // 成功添加一个规则后进行一次计算
                    await RefreshAll();
                };
            }

            editorWindow.ShowDialog();
        }

        /// <summary>
        /// 通用的寻找父级元素的辅助方法
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="child"></param>
        /// <returns></returns>
        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null && child is not T)
            {
                child = VisualTreeHelper.GetParent(child);
            }
            return child as T;
        }

        /// <summary>
        /// 数据列表双击事件
        /// </summary>
        /// <param name="e"></param>
        [RelayCommand]
        public void DataGridBlankAreaDoubleClick(MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as DependencyObject;

            if (element == null) return;

            // --- 新增：拦截约束 ---
            // 如果双击的是 CheckBox 或者其内部组件，直接返回
            if (FindParent<CheckBox>(element) != null)
            {
                return;
            }

            // 1. 尝试寻找点击位置下方的“行”容器
            var row = FindParent<DataGridRow>(element);

            if (row != null)
            {
                // --- 情况 A：点在了行上 ---
                // row.Item 就是这一行绑定的数据对象（BaseRule）
                if (row.Item is BaseRule selectedRule)
                {
                    OpenRuleEditor(selectedRule); // 带参数打开，进入“编辑模式”
                }
            }
            else
            {
                // --- 情况 B：没点在行上 ---
                // 排除掉表头，剩下的就是空白区域
                bool isClickOnHeader = FindParent<DataGridColumnHeader>(element) != null;

                if (!isClickOnHeader)
                {
                    OpenRuleEditor(); // 不带参数打开，进入“新增模式”
                }
            }
        }

        /// <summary>
        /// Rule 表鼠标点击事件
        /// </summary>
        [RelayCommand]
        private void RuleDataGridMouseDown(MouseButtonEventArgs? e = null)
        {
            // 获取点击的目标元素（通过事件参数）
            var originalSource = e?.OriginalSource as DependencyObject;

            // 左键点击时
            if (e?.ChangedButton == MouseButton.Left)
            {
                // 向上寻找是否有 DataGridRow 或 DataGridColumnHeader（防止点表头也清空）
                var isClickOnRow = FindParent<DataGridRow>(originalSource) != null;
                var isClickOnHeader = FindParent<DataGridColumnHeader>(originalSource) != null;

                // 如果既没点到行，也没点到表头，说明点在空白处
                if (!isClickOnRow && !isClickOnHeader)
                {
                    CleanRuleSelection();
                }

            }
        }

        /// <summary>
        /// File 表鼠标点击事件
        /// </summary>
        [RelayCommand]
        private void FileDataGridMouseDown(MouseButtonEventArgs? e = null)
        {
            // 获取点击的目标元素（通过事件参数）
            var originalSource = e?.OriginalSource as DependencyObject;

            // 左键点击时
            if (e?.ChangedButton == MouseButton.Left)
            {
                // 向上寻找是否有 DataGridRow 或 DataGridColumnHeader（防止点表头也清空）
                var isClickOnRow = FindParent<DataGridRow>(originalSource) != null;
                //var isClickOnHeader = FindParent<DataGridColumnHeader>(originalSource) != null;
                // 如果既没点到行，也没点到表头，说明点在空白处
                if (!isClickOnRow)
                {
                    CleanFileSelection();
                }
            }

        }

        /// <summary>
        /// 清除规则选中状态
        /// </summary>
        private void CleanRuleSelection()
        {
            SelectedRules.Clear();
        }

        /// <summary>
        /// 清除文件选中状态
        /// </summary>
        private void CleanFileSelection()
        {
            SelectedFiles.Clear();
        }


        private bool CanClearRule() => Rules.Any();
        /// <summary>
        /// 清空规则事件
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanClearRule))]
        private void ClearRule()
        {
            Rules.Clear();
            _ = RefreshAll();
        }

        /// <summary>
        /// 触发 DropOver 文件时的回调 (隧道事件)
        /// </summary>
        [RelayCommand]
        private void PreviewDragOverFiles(DragEventArgs e)
        {
            // 解决和 GongSolutions.Wpf.DragDrop 的冲突
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }


        /// <summary>
        /// 触发 Drop 文件时的回调 (隧道事件)
        /// </summary>
        [RelayCommand]
        private async Task PreviewDroppedFiles(object? e)
        {
            // 提取原始路径
            if (e is not DragEventArgs args || !args.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var rawPaths = (string[])args.Data.GetData(DataFormats.FileDrop);

            await LoadFile(rawPaths);
        }

        /// <summary>
        /// 加载文件
        /// </summary>
        /// <param name="rawPaths"></param>
        /// <returns></returns>
        private async Task LoadFile(string[] rawPaths)
        {
            ProgressValue = 0;
            IsProgressVisible = true;
            StatusMessage = "准备扫描...";

            var progressHandler = new Progress<int>(percent =>
            {
                ProgressValue = percent;
                StatusMessage = $"正在扫描... {percent}%";
            });

            // 扫描路径
            var validPaths = await FileService.ScanPathsAsync(rawPaths, new() { /* 你的配置 */ });

            if (validPaths == null || validPaths.Count == 0)
            {
                IsProgressVisible = false;
                return;
            }

            // 后台批量处理：去重 + 创建对象
            var toAdd = await Task.Run(() =>
            {
                // 提取现有路径（注意：如果 Files 很大，建议在 VM 里维护一个同步的 HashSet）
                var existing = new HashSet<string>(Files.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);

                return validPaths
                    .Where(p => !existing.Contains(p))
                    .Select(p => new ReNameFile(p))
                    .ToList();
            });

            // 批量更新 UI（核心优化点）
            if (toAdd.Count > 0)
            {
                StatusMessage = $"正在同步 {toAdd.Count} 个项目到列表...";

                // 关键：如果数据量极大，直接替换集合引用比 foreach.Add 快几十倍
                var newList = new List<ReNameFile>(Files);
                newList.AddRange(toAdd);

                // 替换整个集合触发一次总刷新
                Files = new ObservableCollection<ReNameFile>(newList);
            }

            StatusMessage = "加载完成";
            IsProgressVisible = false;

            // 触发你之前的刷新逻辑（预览重命名结果）
            await RefreshAll();
        }

        private bool CanClearFile() => Files.Any();

        /// <summary>
        /// 清空文件
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanClearFile))]
        private void ClearFile()
        {
            Files.Clear();
            _ = RefreshAll();
        }

        // 预览重命名结果
        [RelayCommand]
        private async Task PreviewRename(CancellationToken token = default)
        {
            // 如果外部没有传 token（比如手动点击预览按钮），则逻辑正常跑完
            // 如果是通过 RefreshAll 调用的，token 会在新的请求进来时变更为 Cancel 状态

            try
            {
                ProgressValue = 0;
                IsProgressVisible = true;

                var progressHandler = new Progress<int>(percent =>
                {
                    ProgressValue = percent;
                    StatusMessage = $"预览计算中... {percent}%";
                });

                // 核心：将 token 传给引擎
                // 引擎内部需要在循环中调用 token.ThrowIfCancellationRequested()
                await ReNameEngine.ExecuteAsync(SortedFiles, Rules, progressHandler, token);

                ProgressValue = 100;
                StatusMessage = "预览已更新";
            }
            catch (OperationCanceledException)
            {
                // 当计算被取消时，不需要报错，也不需要更新状态为“准备就绪”
                // 因为下一个 RefreshAll 请求马上就会接管这里
                Debug.WriteLine("预览计算已取消");
                throw; // 向上抛出，让 RefreshAll 的 catch 块捕获
            }
            finally
            {
                // 只有在没有被取消的情况下，才隐藏进度条
                if (!token.IsCancellationRequested)
                {
                    IsProgressVisible = false;
                    ApplyRenameCommand.NotifyCanExecuteChanged();
                }
            }

        }

        // 编写检查逻辑：至少有一个 IsOK 为 true
        private bool CanExecuteApplyRename()
        {
            return Files != null && Files.Any(f => f.IsOK);
        }

        // 应用重命名结果
        [RelayCommand(CanExecute = nameof(CanExecuteApplyRename))]
        private async Task ApplyRename()
        {
            // 调用 HasConflicts 检测重命名是否存在冲突
            if (ReNameEngine.HasConflicts(Files))
            {

                var result = HandyControl.Controls.MessageBox.Show(
                    "当前列表存在命名冲突。点击【确定】将跳过冲突项并重命名其余文件，点击【取消】中止操作。",
                    "发现命名冲突",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel)
                {
                    return; // 用户选取消，直接退出
                }
            }

            ReNameEngine.ExecuteRename(Files);

            // 刷新引擎
            await RefreshAll();
        }

        /// <summary>
        /// 当任意 File 的 Enable 状态被切换时
        /// </summary>
        [RelayCommand]
        private async Task ToggleFileEnable()
        {
            //await RefreshAll();
            await PreviewRename();

        }

        /// <summary>
        /// 当任意 Rule 的 Enable 状态被切换时
        /// </summary>
        [RelayCommand]
        private async Task ToggleRuleEnable()
        {
            //await RefreshAll();
            await PreviewRename();

        }

        private bool CanRemoveSelectedRules() => SelectedRules.Any();

        /// <summary>
        /// 移除规则
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRemoveSelectedRules))]
        private async Task RemoveRule()
        {
            if (SelectedRules.Count == 0) return;

            var remainingItems = await Task.Run(() =>
                Rules.Where(f => !SelectedRules.Contains(f)).ToList());

            var view = CollectionViewSource.GetDefaultView(Rules);

            // 备份当前的排序信息
            var sortBackup = view.SortDescriptions.ToList();

            // 暂时清空排序（防止删除时 DataGrid 频繁重排）
            view.SortDescriptions.Clear();

            // 屏蔽 RefreshAll
            Processing = true;

            // 执行替换（此处 DataGrid 会收到 Reset 通知）
            Rules.Clear();
            foreach (var item in remainingItems) Rules.Add(item);

            // 还原排序
            foreach (var sd in sortBackup) view.SortDescriptions.Add(sd);

            Processing = false;
            SelectedRules.Clear();
        }

        /// <summary>
        /// 编辑规则
        /// </summary>
        /// <param name="rule"></param>
        /// <returns></returns>
        [RelayCommand]
        private void EditRule(BaseRule rule) // 或者是你定义的 Rule 类名
        {
            if (rule != null && Rules.Contains(rule))
            {
                OpenRuleEditor(rule);
            }
        }


        private bool CanRemoveFile() => SelectedFiles.Any();

        /// <summary>
        /// 移除File
        /// </summary>
        /// <param name="rule"></param>
        /// <returns></returns>
        [RelayCommand(CanExecute = nameof(CanRemoveFile))]
        private async Task RemoveFile()
        {
            if (SelectedFiles.Count == 0) return;

            var remainingItems = await Task.Run(() =>
                Files.Where(f => !SelectedFiles.Contains(f)).ToList());

            var view = CollectionViewSource.GetDefaultView(Files);

            // 备份当前的排序信息
            var sortBackup = view.SortDescriptions.ToList();

            // 暂时清空排序（防止删除时 DataGrid 频繁重排）
            view.SortDescriptions.Clear();

            // 屏蔽 RefreshAll
            Processing = true;

            // 执行替换（此处 DataGrid 会收到 Reset 通知）
            Files.Clear();
            foreach (var item in remainingItems) Files.Add(item);

            // 还原排序
            foreach (var sd in sortBackup) view.SortDescriptions.Add(sd);

            Processing = false;
            SelectedFiles.Clear();
        }

        /// <summary>
        /// 加载预设
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task LoadPresets()
        {
            string? currnetPresetName = null;
            if (CurrentPreset != null)
            {
                currnetPresetName = CurrentPreset.Name;
            }
            Presets = [.. await PresetService.GetAllPresetsAsync(PresetDir)];

            if (currnetPresetName != null)
            {
                CurrentPreset = Presets.ToList().Find(x => x.Name == currnetPresetName) ?? new Preset() { Name = "新预设" };
            }
        }

        /// <summary>
        /// 尝试重新加载指定预设
        /// </summary>
        /// <param name="preset">指定预设</param>
        /// <returns></returns>
        [RelayCommand]
        private async Task ReloadPreset(Preset preset)
        {
            if (preset == null) return;

            // 获取新数据
            var p = await PresetService.GetPresetByNameAsync(PresetDir, preset.Name);
            if (p == null) return;

            // 找到旧预设在集合中的索引
            int index = Presets.IndexOf(preset);

            if (index != -1)
            {
                // 替换集合中的元素
                // 这会触发 ObservableCollection 的 CollectionChanged 事件，UI 会自动刷新
                Presets[index] = p;

                CurrentPreset = p;

                //Debug.WriteLine($"成功重载预设并同步至 UI：{p.Name}");
            }
        }


        private bool CanSavePreset() => Rules.Count > 0;

        /// <summary>
        /// 保存预设
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSavePreset))]
        private async Task SavePreset()
        {
            if (CurrentPreset == null) return;

            var preset = CurrentPreset.Clone();

            // 判断预设是否存在于本地磁盘中
            var presetPath = Path.Combine(PresetDir, $"{preset.Name}.json");

            if (File.Exists(presetPath))
            {
                // 如果当前规则已存在则进行覆盖
                var result = HandyControl.Controls.MessageBox.Show(
                     "确认保存修改？",
                     "保存预设",
                     MessageBoxButton.YesNo,
                     MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // 保存前记录最新规则
                    preset.Rules = [.. Rules.Select(r => r.Clone())];

                    if (await FileService.SaveTextAsync(presetPath, preset.ToJson()))
                    {
                        Debug.WriteLine($"成功保存预设：{preset.Name}.json");
                        Debug.WriteLine($"路径：{presetPath}");

                        await LoadPresets();
                    }
                    else
                    {
                        Debug.WriteLine($"保存失败");
                    }
                }
                else
                {
                    Debug.WriteLine($"已取消操作");
                }
            }
            else
            {
                // 先向当前方案存入当前规则列表的规则
                // 弹窗让用户确认预设名
                var name = DialogService.ShowFileNameInputDialogAsync(preset.Name, "保存预设", "输入预设名称：");
                if (name != null)
                {
                    Debug.WriteLine($"准备保存预设：{name}.json");
                    var savePath = Path.Combine(PresetDir, $"{name}.json");
                    preset.Name = name;
                    // 保存前记录最新规则
                    preset.Rules = [.. Rules.Select(r => r.Clone())];

                    if (await FileService.SaveTextAsync(savePath, preset.ToJson()))
                    {
                        Debug.WriteLine($"成功保存预设：{name}.json");
                        Debug.WriteLine($"路径：{savePath}");

                        await LoadPresets();
                    }
                    else
                    {
                        Debug.WriteLine($"保存失败");
                    }

                }
                else
                {
                    Debug.WriteLine($"已取消操作");
                }
            }

        }

        /// <summary>
        /// 另存预设
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSavePreset))]
        private async Task SaveAsPreset()
        {
            if (CurrentPreset == null) return;

            var preset = CurrentPreset.Clone();

            var dialog = new SaveFileDialog
            {
                Title = "预设另存为",
                Filter = "Preset File (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".json",
                AddExtension = true,
                FileName = preset.Name ?? "新预设",
                DefaultDirectory = PresetDir
            };

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                string path = dialog.FileName;
                string name = Path.GetFileNameWithoutExtension(path);

                preset.Name = name;
                // 保存前记录最新规则
                preset.Rules = [.. Rules.Select(r => r.Clone())];

                // 保存逻辑
                if (await FileService.SaveTextAsync(path, preset.ToJson()))
                {
                    Debug.WriteLine($"成功保存预设：{name}.json");
                    Debug.WriteLine($"路径：{path}");

                    await LoadPresets();
                }
                else
                {
                    Debug.WriteLine($"保存失败");
                }
            }
            else
            {
                Debug.WriteLine($"已取消操作");
            }
        }


        private bool IsPresetInSet() => Presets.Contains(CurrentPreset);

        /// <summary>
        /// 重命名预设
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsPresetInSet))]

        private async Task RenamePreset()
        {
            if (CurrentPreset == null) return;
            var name = CurrentPreset.Name;
            // 获取规则名称列表
            var nameList = Presets.Select(x => x.Name).ToList();
            // 排除掉当前规则名称
            nameList.Remove(name);

            var newName = DialogService.ShowFileNameInputDialogAsync(CurrentPreset.Name, "重命名预设", "输入预设名称：", disableNames: nameList);

            if (newName == null || newName.Trim() == name.Trim()) return;

            // 拿到当前规则路径
            var path = Path.Combine(PresetDir, $"{name}.json");
            // 合成新路径
            var newPath = Path.Combine(PresetDir, $"{newName}.json");

            // 删掉旧文件
            if (await FileService.DeleteFileAsync(path))
            {
                // 删除成功后重新写入文件
                CurrentPreset.Name = newName;

                // 保存逻辑
                if (await FileService.SaveTextAsync(path, CurrentPreset.ToJson()))
                {
                    Debug.WriteLine($"成功保存预设：{newName}.json");
                    Debug.WriteLine($"路径：{newPath}");

                    await LoadPresets();
                }
                else
                {
                    Debug.WriteLine($"保存失败");
                }
            }
            else
            {
                Debug.WriteLine($"文件删除失败！\n {path}");
            }

            if (await FileService.MoveFileAsync(path, newPath))
            {
                await LoadPresets();
                // 重新到列表中找到这个预设并选中
                var preset = Presets.ToList().Find(x => x.Name == newName);
                if (preset != null)
                {
                    CurrentPreset = preset;
                }
            }

        }

        /// <summary>
        /// 删除预设
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsPresetInSet))]
        private async Task RemovePreset()
        {
            if (CurrentPreset == null) return;

            var result = HandyControl.Controls.MessageBox.Show(
                    $"确认删除预设 \"{CurrentPreset.Name}.json\" ？(此操作无法恢复)",
                    "删除预设",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                if (await PresetService.RemovePreset(PresetDir, CurrentPreset.Name))
                {
                    Debug.WriteLine("删除成功");
                    Presets.Remove(CurrentPreset);
                    CurrentPreset = new Preset() { Name = "新预设" };
                }
                else
                {
                    Debug.WriteLine("删除失败");
                }
            }
            else
            {
                Debug.WriteLine("取消操作");
            }
        }

        public MainWindowViewModel()
        {
            _ = LoadPresets();

            StatusMessage = "准备就绪";
        }

        private void NotifyCommands()
        {
            ClearFileCommand.NotifyCanExecuteChanged();
            ClearRuleCommand.NotifyCanExecuteChanged();
            SavePresetCommand.NotifyCanExecuteChanged();
            SaveAsPresetCommand.NotifyCanExecuteChanged();
            RenamePresetCommand.NotifyCanExecuteChanged();
            RemovePresetCommand.NotifyCanExecuteChanged();
        }

        private CancellationTokenSource? _refreshCts;


        // 刷新中心
        private async Task RefreshAll()
        {
            // 1. 取消上一次的“等待”和“执行”
            _refreshCts?.Cancel();
            _refreshCts = new CancellationTokenSource();
            var token = _refreshCts.Token;

            try
            {
                // 2. 防抖等待
                await Task.Delay(200, token);

                // 3. 业务锁改为“重入检查”而不是直接 return
                // 这里的 Processing 主要用于 UI 状态显示（比如转圈圈）
                Processing = true;

                // 4. 正式逻辑
                OnPropertyChanged(nameof(Rules));
                OnPropertyChanged(nameof(Files));
                NotifyCommands();

                if (Files != null && Files.Any())
                {
                    // 将 token 传入，让预览逻辑也可以被中途取消
                    await PreviewRename(token);
                }
            }
            catch (OperationCanceledException)
            {
                // 静默退出
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshAll Error: {ex.Message}");
            }
            finally
            {
                // 只有当这是最后一个活跃的任务时，才关闭 Processing 状态
                if (!token.IsCancellationRequested)
                {
                    Processing = false;
                }
            }
        }

        /// <summary>
        /// 物理顺序同步方法
        /// </summary>
        private void SyncPhysicalOrder<T>(ObservableCollection<T> physicalList, List<T> visualOrder)
        {
            for (int i = 0; i < visualOrder.Count; i++)
            {
                var item = visualOrder[i];
                int oldIndex = physicalList.IndexOf(item);
                if (oldIndex != -1 && oldIndex != i)
                {
                    physicalList.Move(oldIndex, i);
                }
            }
        }

        /// <summary>
        /// 自定义拖拽悬浮行为
        /// </summary>
        /// <param name="dropInfo"></param>
        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            var data = dropInfo.Data;
            bool isCompatible = data is ReNameFile || data is BaseRule ||
                                (data is IEnumerable e && e.Cast<object>().Any(x => x is ReNameFile || x is BaseRule));

            if (isCompatible)
            {
                if (dropInfo.VisualTarget is DataGrid dg)
                {
                    var view = CollectionViewSource.GetDefaultView(dg.ItemsSource);

                    // 只有当 SortDescriptions 确实存在时才进入
                    if (view.SortDescriptions.Count > 0)
                    {
                        // 提取视觉顺序
                        var visualItems = dg.Items.Cast<object>().ToList();
                        if (visualItems.Count > 0)
                        {
                            // 执行固化（根据类型分流）
                            var firstItem = visualItems[0];
                            if (firstItem is ReNameFile)
                                SyncPhysicalOrder(Files, visualItems.Cast<ReNameFile>().ToList());
                            else if (firstItem is BaseRule)
                                SyncPhysicalOrder(Rules, visualItems.Cast<BaseRule>().ToList());

                            // 关键：立即清空排序描述，这样下一次 DragOver 触发时 
                            // view.SortDescriptions.Count 就变成 0 了，不会再进这个 if 块
                            view.SortDescriptions.Clear();

                            // 重置列头状态（去掉那个小箭头）
                            foreach (var col in dg.Columns)
                            {
                                if (col.SortDirection != null)
                                    col.SortDirection = null;
                            }

                            // 5. 刷新视图
                            dg.Items.Refresh();

                            Debug.WriteLine("排序已固化为物理顺序");
                        }
                    }
                }

                dropInfo.Effects = DragDropEffects.Move;
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            }
        }

        /// <summary>
        /// 自定义拖拽放置
        /// </summary>
        void IDropTarget.Drop(IDropInfo dropInfo)
        {
            // 根据拖拽对象类型，提前快照记录选中项
            // 这里假设 dropInfo.Data 是我们要移动的数据
            List<ReNameFile> selectedFilesSnapshot = [.. SelectedFiles];
            List<BaseRule> selectedRulesSnapshot = [.. SelectedRules];

            // 执行物理移动（这会导致集合顺序重排）
            GongSolutions.Wpf.DragDrop.DragDrop.DefaultDropHandler.Drop(dropInfo);


            // 提升用户体验
            if (dropInfo.VisualTarget is DataGrid dg)
            {
                dg.Focus();

                // 恢复视觉焦点：ScrollIntoView 不能传集合，传第一个选中的项即可
                var firstSelected = selectedFilesSnapshot?.FirstOrDefault() as object
                                  ?? selectedRulesSnapshot?.FirstOrDefault();

                if (firstSelected != null)
                {
                    dg.ScrollIntoView(firstSelected);
                }
            }

            _ = RefreshAll();

            // 恢复选中状态
            // 注意：如果是多选，通常需要重新赋值给绑定的选中集合
            if (dropInfo.Data is ReNameFile || (dropInfo.Data is IEnumerable ef && ef.Cast<object>().Any(x => x is ReNameFile)))
            {
                if (selectedFilesSnapshot != null)
                {
                    // 重新填回你的选中记录集合
                    SelectedFiles.Clear();

                    foreach (var item in selectedFilesSnapshot)
                    {
                        SelectedFiles.Add(item);
                    }
                }
            }
            else if (dropInfo.Data is BaseRule || (dropInfo.Data is IEnumerable er && er.Cast<object>().Any(x => x is BaseRule)))
            {
                if (selectedRulesSnapshot != null)
                {
                    SelectedRules.Clear();

                    foreach (var item in selectedRulesSnapshot)
                    {
                        SelectedRules.Add(item);
                    }
                }
            }
        }
    }
}
