using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GongSolutions.Wpf.DragDrop;
using HandyControl.Controls;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using ReNamer.Comparers;
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

namespace ReNamer.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject, IDropTarget
    {
        public MainWindowViewModel()
        {
            _ = LoadPresets();

            StatusMessage = "准备就绪";
        }

        /// <summary>
        /// 版本
        /// </summary>
        [ObservableProperty]
        private string _version = "v0.1.4-beta";

        /// <summary>
        /// 标题
        /// </summary>
        public string Title => $"批量重命名工具 {Version}";

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

        // 规则列表
        [ObservableProperty]

        private ObservableCollection<BaseRule> _rules = [];


        partial void OnRulesChanged(ObservableCollection<BaseRule>? oldValue, ObservableCollection<BaseRule> newValue)
        {
            // 清理旧集合的监听
            if (oldValue != null)
            {
                oldValue.CollectionChanged -= OnRules_CollectionChanged;

                // 获取旧视图并清理 PropertyChanged
                var oldView = CollectionViewSource.GetDefaultView(oldValue);
                if (oldView is INotifyPropertyChanged npc)
                    npc.PropertyChanged -= OnViewRules_PropertyChanged;
            }

            // 挂载新集合的监听
            if (newValue != null)
            {
                // 监听数据层：Add, Remove, Reset (处理 CanUserDeleteRows)
                newValue.CollectionChanged += OnRules_CollectionChanged;

                // 获取新视图
                var view = CollectionViewSource.GetDefaultView(newValue);

                // 监听排序层：点击表头 SortDescriptions 变化
                if (view is INotifyPropertyChanged npc)
                {
                    npc.PropertyChanged += OnViewRules_PropertyChanged;
                }
            }

            NotifyCommands();
        }

        private async void OnRules_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isRefreshing) return;
            //Debug.WriteLine("OnRules_CollectionChanged");
            await RefreshAll();
        }

        private async void OnViewRules_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isRefreshing) return;
            //Debug.WriteLine("OnViewRules_PropertyChanged");
            await RefreshAll();
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

        // 文件列表
        [ObservableProperty]
        private ObservableCollection<ReNameFile> _files = [];

        partial void OnFilesChanged(ObservableCollection<ReNameFile>? oldValue, ObservableCollection<ReNameFile> newValue)
        {
            // 清理旧集合的监听
            if (oldValue != null)
            {
                oldValue.CollectionChanged -= OnFiles_CollectionChanged;

                // 获取旧视图并清理 PropertyChanged
                var oldView = CollectionViewSource.GetDefaultView(oldValue);
                if (oldView is INotifyPropertyChanged npc)
                    npc.PropertyChanged -= OnViewFiles_PropertyChanged;
            }

            // 挂载新集合的监听
            if (newValue != null)
            {
                // 监听数据层：Add, Remove, Reset (处理 CanUserDeleteRows)
                newValue.CollectionChanged += OnFiles_CollectionChanged;

                // 获取新视图
                var view = CollectionViewSource.GetDefaultView(newValue);

                // 监听排序层：点击表头 SortDescriptions 变化
                if (view is INotifyPropertyChanged npc)
                {
                    npc.PropertyChanged += OnViewFiles_PropertyChanged;
                }
            }

            NotifyCommands();
        }


        private async void OnFiles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isRefreshing) return;
            //Debug.WriteLine("OnFiles_CollectionChanged");
            await RefreshAll();
        }

        private async void OnViewFiles_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isRefreshing) return;
            //Debug.WriteLine("OnViewFiles_PropertyChanged");

            if (sender is not ListCollectionView view)
                return;

            // 只处理排序变化
            if (view.SortDescriptions.Count > 0)
            {
                var sort = view.SortDescriptions[0];

                // 拦截默认排序
                view.SortDescriptions.Clear();

                Debug.WriteLine(sort.PropertyName);

                // 替换为自然排序
                view.CustomSort = new CustomSortComparer(
                    sort.PropertyName,
                    sort.Direction
                );
            }

            await RefreshAll();
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
        /// 刷新标识符
        /// </summary>
        private bool _isRefreshing;

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

            // 如果双击的是 CheckBox 或者其内部组件，直接返回
            if (FindParent<CheckBox>(element) != null)
            {
                return;
            }

            // 尝试寻找点击位置下方的“行”容器
            var row = FindParent<DataGridRow>(element);

            if (row != null)
            {
                // 当点击在了行上
                if (row.Item is BaseRule selectedRule)
                {
                    OpenRuleEditor(selectedRule); // 带参数打开，进入“编辑模式”
                }
            }
            else
            {
                // 当点击没在行上
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
            var validPaths = await FileService.ScanPathsAsync(rawPaths, new()
            {
                IsRecursive = true,
                Progress = progressHandler,
                IncludeFiles = CurrentPreset.Filters.File.Enable,
                FileRegex = RegexUtils.SafeCreateRegex(CurrentPreset.Filters.File.Regex),
                IncludeDirectories = CurrentPreset.Filters.Folder.Enable,
                DirectoryRegex = RegexUtils.SafeCreateRegex(CurrentPreset.Filters.Folder.Regex),
            });

            if (validPaths == null || validPaths.Count == 0)
            {
                IsProgressVisible = false;
                return;
            }

            // 后台批量处理：去重 + 创建对象
            var toAdd = await Task.Run(() =>
            {
                // 提取现有路径
                var existing = new HashSet<string>(Files.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);

                return validPaths
                    .Where(p => !existing.Contains(p))
                    .Select(p => new ReNameFile(p))
                    .ToList();
            });

            if (toAdd.Count > 0)
            {
                StatusMessage = $"正在同步 {toAdd.Count} 个项目到列表...";

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

                // 将 token 传给引擎，引擎内部需要在循环中调用 token.ThrowIfCancellationRequested()
                await ReNameEngine.ExecuteAsync(SortedFiles, Rules, progressHandler, token);

                ProgressValue = 100;
                StatusMessage = "预览已更新";
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("预览计算已取消");
                throw;
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

        private bool CanExecuteApplyRename()
        {
            return Files != null && Files.Any(f => f.IsOK);
        }

        /// <summary>
        /// 应用重命名结果
        /// </summary>
        /// <returns></returns>
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
            await RefreshAll();
        }

        /// <summary>
        /// 当任意 Rule 的 Enable 状态被切换时
        /// </summary>
        [RelayCommand]
        private async Task ToggleRuleEnable()
        {
            await RefreshAll();
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

            // 暂时清空排序
            view.SortDescriptions.Clear();

            // 标记为正在执行
            Processing = true;

            // 执行替换
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
        private void EditRule(BaseRule rule)
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

            // 暂时清空排序
            view.SortDescriptions.Clear();

            // 标记为正在执行
            Processing = true;

            // 执行替换
            Files.Clear();
            foreach (var item in remainingItems) Files.Add(item);

            // 还原排序
            foreach (var sd in sortBackup) view.SortDescriptions.Add(sd);

            Processing = false;
            SelectedFiles.Clear();
        }

        private bool CanOpenFile() => SelectedFiles.Any();

        /// <summary>
        /// 打开文件
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanOpenFile))]
        private void OpenFile()
        {
            if (SelectedFiles.Count == 0) return;
            // 默认只使用第一个路径
            var first = SelectedFiles.ElementAt(0);
            if (first == null) return;
            FileService.OpenPath(first.Path);
        }

        /// <summary>
        /// 打开文件所在目录并选中文件
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanOpenFile))]
        private void OpenFileAndSelect()
        {
            if (SelectedFiles.Count == 0) return;
            // 默认只使用第一个路径
            var first = SelectedFiles.ElementAt(0);
            if (first == null) return;
            FileService.OpenAndSelect(first.Path);
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
                Presets[index] = p;
                CurrentPreset = p;
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
        private void RemovePreset()
        {
            if (CurrentPreset == null) return;
            var result = HandyControl.Controls.MessageBox.Show(
                    $"确认删除预设 \"{CurrentPreset.Name}.json\" ？(此操作无法恢复)",
                    "删除预设",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                if (PresetService.RemovePreset(PresetDir, CurrentPreset.Name))
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

        private void NotifyCommands()
        {
            ClearFileCommand.NotifyCanExecuteChanged();
            ClearRuleCommand.NotifyCanExecuteChanged();
            SavePresetCommand.NotifyCanExecuteChanged();
            SaveAsPresetCommand.NotifyCanExecuteChanged();
            RenamePresetCommand.NotifyCanExecuteChanged();
            RemovePresetCommand.NotifyCanExecuteChanged();
        }


        /// <summary>
        /// 刷新取消令牌
        /// </summary>
        private CancellationTokenSource? _refreshCts;

        // 刷新
        private async Task RefreshAll()
        {

            //Debug.WriteLine("RefreshAll triggered");

            // 创建一个取消令牌
            var newCts = new CancellationTokenSource();
            // 替换当前的 CancellationTokenSource，获取旧实例用于取消之前的刷新任务 （线程安全操作）
            var oldCts = Interlocked.Exchange(ref _refreshCts, newCts);
            // 取消之前未完成的刷新任务
            oldCts?.Cancel();
            // 释放 取消令牌
            oldCts?.Dispose();

            // 获取当前刷新对应的取消令牌
            var token = newCts.Token;

            _isRefreshing = true;   // 屏蔽刷新触发

            try
            {
                // 防抖延迟（期间如果有新请求，会通过取消 token 中断）
                await Task.Delay(200, token);

                Processing = true;


                NotifyCommands();

                if (Files.Any())
                {
                    await PreviewRename(token);
                }
            }
            catch (OperationCanceledException)
            {
                // 被新请求顶掉
                return;
            }
            finally
            {
                // 只有当前仍是“最新的一次刷新”且未被取消，才允许收尾
                if (_refreshCts == newCts && !token.IsCancellationRequested)
                {
                    Processing = false;
                    _isRefreshing = false;  // 刷新结束，允许后续刷新触发
                }
            }
        }

        /// <summary>
        /// 同步物理顺序
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

                    if (view is ListCollectionView listView && listView.CustomSort != null)
                    {

                        // 拿到当前排序结果
                        var visualItems = dg.Items.Cast<object>().ToList();

                        if (visualItems.Count > 0)
                        {

                            // 固化排序 (分别处理文件列表和规则列表)
                            var firstItem = visualItems[0];

                            if (firstItem is ReNameFile)
                                SyncPhysicalOrder(Files, visualItems.Cast<ReNameFile>().ToList());
                            else if (firstItem is BaseRule)
                                SyncPhysicalOrder(Rules, visualItems.Cast<BaseRule>().ToList());

                            // 清空 SortDescriptions，防止重复进入当前执行逻辑
                            view.SortDescriptions.Clear();
                            listView.CustomSort = null;

                            // 重置列头状态
                            foreach (var col in dg.Columns)
                            {
                                col.SortDirection = null;
                            }

                            // 刷新视图
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
            // 提前记录选中项
            List<ReNameFile> selectedFilesSnapshot = [.. SelectedFiles];
            List<BaseRule> selectedRulesSnapshot = [.. SelectedRules];

            // 执行物理移动
            GongSolutions.Wpf.DragDrop.DragDrop.DefaultDropHandler.Drop(dropInfo);

            if (dropInfo.VisualTarget is DataGrid dg)
            {
                dg.Focus();

                // 恢复视觉焦点，ScrollIntoView 不能传集合，传第一个选中的项即可
                var firstSelected = selectedFilesSnapshot?.FirstOrDefault() as object
                                  ?? selectedRulesSnapshot?.FirstOrDefault();

                if (firstSelected != null)
                {
                    dg.ScrollIntoView(firstSelected);
                }
            }

            // 触发刷新
            _ = RefreshAll();

            // 恢复选中状态
            if (dropInfo.Data is ReNameFile || (dropInfo.Data is IEnumerable ef && ef.Cast<object>().Any(x => x is ReNameFile)))
            {
                if (selectedFilesSnapshot != null && selectedFilesSnapshot.Any())
                {
                    SelectedFiles.Clear();

                    foreach (var item in selectedFilesSnapshot)
                    {
                        SelectedFiles.Add(item);
                    }
                }
            }
            else if (dropInfo.Data is BaseRule || (dropInfo.Data is IEnumerable er && er.Cast<object>().Any(x => x is BaseRule)))
            {
                if (selectedRulesSnapshot != null && selectedRulesSnapshot.Any())
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
