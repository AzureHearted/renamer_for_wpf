using ReNamer.Models.ReName;
using ReNamer.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ReNamer.Engines
{
    /// <summary>
    /// 重命名引擎
    /// </summary>
    public static class ReNameEngine
    {

        /// <summary>
        /// 调度器：按顺序应用所有规则
        /// </summary>
        /// <param name="files">文件列表</param>
        /// <param name="rules">规则列表</param>
        public static void Execute(IEnumerable<ReNameFile> files, IEnumerable<BaseRule> rules)
        {
            // 1. 初始化：每一轮计算前，先重置 NewName 为原始 Name
            // 这样可以保证用户修改规则顺序时，计算结果是重新叠加的，而不是在旧结果上出错
            foreach (var file in files)
            {
                file.NewName = file.Name;
            }

            // 2. 管道式处理：外层循环规则，内层循环文件
            // 这样符合“第一步全局加前缀，第二步全局改后缀”的直觉
            foreach (var rule in rules)
            {
                if (!rule.Enable) continue; // 跳过禁用的规则

                // 根据规则类型分发任务
                InvokeRule(files, rule);
            }

            // 最后进行冲突检测
            CheckConflicts(files);
        }

        /// <summary>
        /// (异步) 调度器：按顺序应用所有规则
        /// </summary>
        /// <param name="files">文件列表</param>
        /// <param name="rules">规则列表</param>
        /// <param name="progress">进度</param>
        /// <param name="token">操作取消令牌</param>
        /// <returns></returns>
        public static async Task ExecuteAsync(
            IEnumerable<ReNameFile> files,
            IEnumerable<BaseRule> rules,
            IProgress<int>? progress = null,
            CancellationToken token = default)
        {
            var fileList = files.ToList();
            // 新开一个后台线程来执行
            await Task.Run(() =>
            {
                int totalSteps = rules.Count(r => r.Enable) + 1;
                int currentStep = 0;

                // 初始化
                foreach (var file in fileList)
                {
                    token.ThrowIfCancellationRequested();
                    file.NewName = file.Name;
                }

                // 按照规则列表顺序处理
                foreach (var rule in rules)
                {
                    if (!rule.Enable)
                    {
                        // 更新进度
                        currentStep++;
                        progress?.Report((currentStep * 100) / totalSteps);
                        continue;
                    }
                    token.ThrowIfCancellationRequested();

                    // 执行具体的规则分发 (这里沿用你原来的 switch 逻辑)
                    InvokeRule(fileList, rule);

                    // 更新进度
                    currentStep++;
                    progress?.Report((currentStep * 100) / totalSteps);
                }

                // 进行冲突检测
                token.ThrowIfCancellationRequested();
                CheckConflicts(fileList);

                progress?.Report(100);
            }, token);
        }

        /// <summary>
        /// 执行规则
        /// </summary>
        /// <param name="files"></param>
        /// <param name="rule"></param>
        private static void InvokeRule(IEnumerable<ReNameFile> files, BaseRule rule)
        {
            switch (rule)
            {
                case InsertRule r: Insert(files, r); break;
                case ReplaceRule r: Replace(files, r); break;
                case RemoveRule r: Remove(files, r); break;
                case SerializeRule r: Serialize(files, r); break;
                case FillRule r: Fill(files, r); break;
                case RegexRule r: Regex(files, r); break;
                case ExtensionRule r: Entension(files, r); break;
            }
        }

        /// <summary>
        /// 模拟父目录重命名对路径产生的影响计算“最终路径”
        /// </summary>
        /// <param name="path"></param>
        /// <param name="dirMap"></param>
        /// <returns></returns>
        private static string ApplyDirectoryRename(string path, Dictionary<string, string> dirMap)
        {
            foreach (var kv in dirMap)
            {
                string oldDir = kv.Key + Path.DirectorySeparatorChar;

                if (path.StartsWith(oldDir, StringComparison.OrdinalIgnoreCase))
                {
                    string relative = path.Substring(oldDir.Length);
                    return Path.Combine(kv.Value, relative);
                }
            }

            return path;
        }

        /// <summary>
        /// 检测重命名结果冲突
        /// </summary>
        public static void CheckConflicts(IEnumerable<ReNameFile> files)
        {
            // 1️⃣ 记录任务中所有目标路径
            var targetPathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 2️⃣ 构建目录 rename 映射表
            var dirRenameMap = files
                .Where(f => f.Enable && f.IsDirectory && f.Path != f.NewPath)
                .ToDictionary(
                    f => f.Path,
                    f => f.NewPath,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                file.IsConflict = false;
                file.ParentDirRenamed = false;

                if (!file.Enable)
                    continue;

                // 计算最终路径（模拟父目录 rename）
                string newPath = ApplyDirectoryRename(file.NewPath, dirRenameMap);

                // 磁盘冲突检测
                bool existsOnDisk;

                if (!file.IsDirectory)
                    existsOnDisk = File.Exists(newPath);
                else
                    existsOnDisk = Directory.Exists(newPath);

                existsOnDisk = (file.Path != newPath) && existsOnDisk;

                // 任务内部冲突检测
                bool existsInTask = targetPathSet.Contains(newPath);


                // 父目录 重命名 检测
                foreach (var dir in dirRenameMap.Keys)
                {
                    if (file.Path.StartsWith(
                        dir + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        file.ParentDirRenamed = true;
                        break;
                    }
                }

                // 标记冲突
                if (existsOnDisk || existsInTask)
                {
                    file.IsConflict = true;
                }

                // 记录任务路径
                targetPathSet.Add(newPath);
            }
        }

        /// <summary>
        /// 检查当前文件列表中是否存在任何启用的冲突项
        /// </summary>
        /// <param name="files">文件列表</param>
        /// <returns>如果存在至少一个冲突项则返回 true</returns>
        public static bool HasConflicts(IEnumerable<ReNameFile> files)
        {
            // 确保状态是最新的
            CheckConflicts(files);

            // 检查是否有任何【已启用】的文件被标记为【冲突】
            return files.Any(f => f.Enable && f.IsConflict);
        }

        /// <summary>
        /// 路径深度计算
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static int GetPathDepth(string path)
        {
            return path.Split(Path.DirectorySeparatorChar).Length;
        }

        /// <summary>
        /// 执行重命名
        /// </summary>
        public static void ExecuteRename(IEnumerable<ReNameFile> files)
        {
            // 过滤可执行项
            var enableFileList = files
                .Where(f => f.Enable && !f.IsConflict && f.Path != f.NewPath)
                .ToList();

            // 没有可执行的文件就停止
            if (enableFileList.Count == 0)
                return;

            // 深路径优先，文件优先
            var ordered = enableFileList
                .OrderByDescending(f => GetPathDepth(f.Path))
                .ThenBy(f => f.IsDirectory)
                .ToList();

            // 构建 rename 映射
            var pathMap = ordered.ToDictionary(
                f => f.Path,
                f => f.NewPath,
                StringComparer.OrdinalIgnoreCase);

            // 解决循环重命名（cycle rename）问题
            foreach (var file in ordered)
            {
                if (pathMap.TryGetValue(file.NewPath, out var target) &&
                    target.Equals(file.Path, StringComparison.OrdinalIgnoreCase))
                {
                    string tempPath = file.Path + ".tmp_" + Guid.NewGuid().ToString("N");

                    if (file.IsDirectory)
                        Directory.Move(file.Path, tempPath);
                    else
                        File.Move(file.Path, tempPath);

                    file.Path = tempPath;

                    // 同步 pathMap
                    pathMap[tempPath] = file.NewPath;
                }
            }

            // 开始正式重命名
            foreach (var file in ordered)
            {
                string source = file.Path;
                string target = file.NewPath;

                try
                {
                    if (file.IsDirectory)
                    {
                        if (!Directory.Exists(source))
                            continue;

                        Directory.Move(source, target);

                        // 更新子路径 (这里必须传入 files 因为就算没启用重命名的 file 父目录也可能会重命名)
                        UpdateChildrenPaths(files, source, target);
                    }
                    else
                    {
                        if (!File.Exists(source))
                            continue;

                        File.Move(source, target);
                    }

                    // 更新当前对象路径
                    file.Path = target;
                    file.UpdateToNewName(target);
                }
                catch
                {
                    file.IsConflict = true;
                }
            }
        }

        /// <summary>
        /// 更新子项目路径
        /// </summary>
        /// <param name="files"></param>
        /// <param name="oldDir"></param>
        /// <param name="newDir"></param>
        private static void UpdateChildrenPaths(IEnumerable<ReNameFile> files, string oldDir, string newDir)
        {
            string prefix = oldDir + Path.DirectorySeparatorChar;

            foreach (var f in files)
            {
                if (f.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string relative = f.Path.Substring(prefix.Length);
                    f.Path = Path.Combine(newDir, relative);
                }

                // 处理目录路径的情况
                if (string.Equals(f.Dir, oldDir, StringComparison.OrdinalIgnoreCase))
                {
                    f.Dir = newDir;
                }
                else if (f.Dir.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string relative = f.Dir.Substring(prefix.Length);
                    f.Dir = Path.Combine(newDir, relative);
                }
            }
        }

        /// <summary>
        /// 插入
        /// </summary>
        /// <param name="list">待重命名列表</param>
        /// <param name="rule">插入规则</param>
        public static void Insert(IEnumerable<ReNameFile> list, InsertRule rule)
        {
            // 拿到要插入的内容
            string content = rule.Content;

            foreach (var item in list)
            {
                // 跳过不允许被修改的项目
                if (!item.Enable)
                    continue;


                // 确定操作的基础文本 (处理扩展名逻辑)
                string text = (item.IsDirectory || !rule.IgnoreExt) ? item.NewName : item.NewNameNoExt;

                switch (rule.Position)
                {
                    case InsertPosition.Prefix:
                        text = content + text;
                        break;

                    case InsertPosition.Suffix:
                        text += content;
                        break;

                    case InsertPosition.Index:
                        text = StringUtils.InsertByIndex(
                            text,
                            content,
                            rule.AnchorIndex,
                            rule.ReverseIndex);
                        break;
                    case InsertPosition.Before:
                        text = StringUtils.InsertByMatch(text, content, rule.BeforeAnchorText, InsertPosition.Before, rule.IgnoreCase);
                        break;
                    case InsertPosition.After:
                        text = StringUtils.InsertByMatch(text, content, rule.AfterAnchorText, InsertPosition.After, rule.IgnoreCase);
                        break;
                    case InsertPosition.Replace:
                        text = content;
                        break;
                }

                // 最后判断是否加上扩展名
                if (!item.IsDirectory && rule.IgnoreExt)
                    item.NewName = text + "." + item.Ext;
                else
                    item.NewName = text;
            }
        }

        /// <summary>
        /// 替换
        /// </summary>
        /// <param name="list">待重命名列表</param>
        /// <param name="rule">插入规则</param>
        public static void Replace(IEnumerable<ReNameFile> list, ReplaceRule rule)
        {
            var match = rule.Match;
            var replaceTo = rule.ReplaceTo;
            var range = rule.Range;

            foreach (var item in list)
            {
                // 跳过不允许被修改的项目
                if (!item.Enable)
                    continue;

                // 确定操作的基础文本 (处理扩展名逻辑)
                string text = (item.IsDirectory || !rule.IgnoreExt) ? item.NewName : item.NewNameNoExt;

                // 如果是目录则直接拿到文件名
                if (item.IsDirectory)
                    text = item.NewName;

                text = StringUtils.Replace(text, match, replaceTo, range, rule.IgnoreCase, rule.IsExactMatch);

                // 最后判断是否加上扩展名
                if (!item.IsDirectory && rule.IgnoreExt)
                    item.NewName = text + "." + item.Ext;
                else
                    item.NewName = text;
            }
        }

        /// <summary>
        /// 移除
        /// </summary>
        /// <param name="list">待重命名列表</param>
        /// <param name="rule">插入规则</param>
        public static void Remove(IEnumerable<ReNameFile> list, RemoveRule rule)
        {
            var match = rule.Match;
            var range = rule.Range;

            foreach (var item in list)
            {
                // 跳过不允许被修改的项目
                if (!item.Enable)
                    continue;

                // 确定操作的基础文本 (处理扩展名逻辑)
                string text = (item.IsDirectory || !rule.IgnoreExt) ? item.NewName : item.NewNameNoExt;

                // 如果是目录则直接拿到文件名
                if (item.IsDirectory)
                    text = item.NewName;

                text = StringUtils.Remove(text, match, range, rule.IgnoreCase, rule.IsExactMatch);

                // 最后判断是否加上扩展名
                if (!item.IsDirectory && rule.IgnoreExt)
                    item.NewName = text + "." + item.Ext;
                else
                    item.NewName = text;
            }
        }

        /// <summary>
        /// 序列化
        /// </summary>
        /// <param name="list">待重命名列表</param>
        /// <param name="rule">插入规则</param>
        public static void Serialize(IEnumerable<ReNameFile> list, SerializeRule rule)
        {
            // 计算最大补零长度 (基于启用项目的总数)
            int enabledItemCount = list.Count(i => i.Enable);

            // 如果没有启用项，则停止后续操作
            if (enabledItemCount == 0) return;

            long startVal = rule.SequenceStart;
            // 计算结束序列的值
            long endVal = (long)(enabledItemCount - 1) * rule.SequenceStep + rule.SequenceStart;

            // 计算自动 Padding 长度
            int autoPaddingLength = Math.Max(
                Math.Abs(startVal).ToString().Length,
                Math.Abs(endVal).ToString().Length
            );

            string? currentDir = null;
            int num = 0;

            foreach (var item in list)
            {
                // 跳过未启用的项
                if (!item.Enable) continue;

                // 检查目录变化以重置计数
                if (currentDir == null || currentDir != item.Dir)
                {
                    // 记录当前目录
                    currentDir = item.Dir;

                    if (rule.ResetFolderChanges)
                        num = 0;
                }

                // 计算当前序列值
                long currentVal = (long)num++ * rule.SequenceStep + rule.SequenceStart;

                // 生成序列文本
                string sequence = Math.Abs(currentVal).ToString();

                // 处理序列补零
                if (rule.PaddingCount > 0)
                {
                    // 固定补零长度
                    sequence = sequence.PadLeft(rule.PaddingCount, '0');
                }
                else if (rule.PaddingCount <= -1)
                {
                    // 自动补零长度
                    sequence = sequence.PadLeft(autoPaddingLength, '0');
                }

                // 处理负数步长的情况
                if (rule.SequenceStep < 0 && currentVal < 0)
                {
                    sequence = "-" + sequence;
                }

                // 确定操作的基础文本 (处理扩展名逻辑)
                string text = (item.IsDirectory || !rule.IgnoreExt) ? item.NewName : item.NewNameNoExt;

                // 根据位置插入序列号
                switch (rule.Position)
                {
                    case InsertPosition.Prefix:
                        text = sequence + text;
                        break;
                    case InsertPosition.Suffix:
                        text = text + sequence;
                        break;
                    case InsertPosition.Index:
                        text = StringUtils.InsertByIndex(text, sequence, rule.AnchorIndex, rule.ReverseIndex);
                        break;
                    case InsertPosition.Before:
                        text = StringUtils.InsertByMatch(text, sequence, rule.BeforeAnchorText, InsertPosition.Before,
                             rule.IgnoreCase, rule.IsExactMatch);
                        break;
                    case InsertPosition.After:
                        text = StringUtils.InsertByMatch(text, sequence, rule.AfterAnchorText, InsertPosition.After,
                             rule.IgnoreCase, rule.IsExactMatch);
                        break;
                    case InsertPosition.Replace:
                        text = sequence;
                        break;
                }

                // 最后判断是否加上扩展名
                if (!item.IsDirectory && rule.IgnoreExt)
                    item.NewName = text + "." + item.Ext;
                else
                    item.NewName = text;
            }
        }


        /// <summary>
        /// 填充
        /// </summary>
        /// <param name="list">待重命名列表</param>
        /// <param name="rule">插入规则</param>
        public static void Fill(IEnumerable<ReNameFile> list, FillRule rule)
        {
            if (list == null || rule == null) return;

            // 匹配数字的正则表达式
            var numberRegex = new Regex(@"(\d+)");

            foreach (var item in list)
            {
                // 跳过未被启用的项
                if (!item.Enable) continue;

                // 确定操作的基础文本 (处理扩展名逻辑)
                string text = (item.IsDirectory || !rule.IgnoreExt) ? item.NewName : item.NewNameNoExt;

                if (rule.RemoveZeroPadding)
                {
                    // 移除所有匹配到的数字序列的前导零
                    text = numberRegex.Replace(text, m =>
                    {
                        string val = m.Value.TrimStart('0');
                        // 如果全是0（如 "000"），TrimStart 后会变空字符串，需保留一个 "0"
                        return string.IsNullOrEmpty(val) ? "0" : val;
                    });
                }
                else if (rule.ZeroPadding.Enable)
                {
                    // 补零
                    text = numberRegex.Replace(text, m =>
                    {
                        string numStr = m.Value;
                        // 如果当前数字长度已经达到或超过目标长度，不处理
                        if (numStr.Length >= rule.ZeroPadding.Length) return numStr;
                        // 仅对数字部分进行补零
                        return numStr.PadLeft(rule.ZeroPadding.Length, '0');
                    });
                }

                // 文本填充
                if (rule.TextPadding.Enable && !string.IsNullOrEmpty(rule.TextPadding.Character))
                {
                    int currentLen = text.Length;

                    int targetLen = rule.TextPadding.Length;

                    if (currentLen < targetLen)
                    {
                        int needLen = targetLen - currentLen;

                        string padStr = rule.TextPadding.Character;

                        // padStr 需要重复的次数 (向上取整)
                        int repeatCount = (needLen / padStr.Length) + 1; // 总长度 / 单个长度，如果有余数就多加 1 组

                        // 生成重复的序列
                        IEnumerable<string> repeatSequence = Enumerable.Repeat(padStr, repeatCount);

                        // 拼接序列
                        string longStr = string.Join("", repeatSequence);

                        // 从长字符串中截取精确的长度
                        string padding = longStr.Substring(0, needLen);

                        text = rule.TextPadding.Direction == PaddingDirection.Left ? padding + text : text + padding;
                    }
                }

                // 最后判断是否加上扩展名
                if (!item.IsDirectory && rule.IgnoreExt)
                    item.NewName = text + "." + item.Ext;
                else
                    item.NewName = text;
            }
        }


        /// <summary>
        /// 正则
        /// </summary>
        /// <param name="list">待重命名列表</param>
        /// <param name="rule">插入规则</param>
        public static void Regex(IEnumerable<ReNameFile> list, RegexRule rule)
        {
            // 如果正则表达式内容为空，则停止执行
            if (string.IsNullOrEmpty(rule.Regex)) return;

            // 配置表达式选项
            RegexOptions options = rule.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;

            // 如果是全字匹配，则修改表达式
            string finalPattern = rule.IsExactMatch ? $@"\b{rule.Regex}\b" : rule.Regex;

            // 创建正则表达式
            Regex regex;
            try
            {
                regex = new Regex(finalPattern, options);
            }
            catch (ArgumentException)
            {
                // 正则表达式语法错误，直接返回不执行
                return;
            }

            foreach (var item in list)
            {
                // 跳过未启用的项
                if (!item.Enable) continue;

                // 确定基础文本
                string text = (item.IsDirectory || !rule.IgnoreExt) ? item.NewName : item.NewNameNoExt;

                // 执行正则替换
                text = regex.Replace(text, rule.ReplaceTo);

                // 最后判断是否加上扩展名
                if (!item.IsDirectory && rule.IgnoreExt)
                    item.NewName = text + "." + item.Ext;
                else
                    item.NewName = text;
            }
        }

        /// <summary>
        /// 扩展名
        /// </summary>
        /// <param name="list">待重命名列表</param>
        /// <param name="rule">插入规则</param>
        public static void Entension(IEnumerable<ReNameFile> list, ExtensionRule rule)
        {
            foreach (var item in list)
            {
                // 跳过未启用项
                if (!item.Enable) continue;

                // 获取基础文本 (处理目录和扩展名逻辑)
                string text = (item.IsDirectory || !rule.IgnoreExt) ? item.NewName : item.NewNameNoExt;

                if (item.IsDirectory)
                {
                    // 如果是目录则直接添加一个扩展名在名称之后
                    text += ("." + rule.Extension);
                }
                else
                {
                    // 如果是文件：
                    if (rule.IgnoreExt)
                    {
                        // 直接添加新扩展名在旧扩展名之后
                        text += ("." + item.Ext + "." + rule.Extension);
                    }
                    else
                    {
                        // 替换扩展名
                        var nameNoExt = Path.GetFileNameWithoutExtension(text);
                        text = nameNoExt + "." + rule.Extension;
                    }
                }

                item.NewName = text;
            }
        }
    }
}
