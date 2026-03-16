using ReNamer.Models.ReName;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ReNamer.Services
{

    public static class FileService
    {

        public class ScanOptions
        {
            /// <summary>
            /// 是否递归扫描子目录
            /// </summary>
            public bool IsRecursive { get; set; } = true;

            /// <summary>
            /// 是否包含文件夹
            /// </summary>
            public bool IncludeDirectories { get; set; } = false;

            /// <summary>
            /// 文件夹匹配正则（为空则全部）
            /// </summary>
            public string? DirectoryRegex { get; set; }

            /// <summary>
            /// 是否包含文件
            /// </summary>
            public bool IncludeFiles { get; set; } = true;

            /// <summary>
            /// 文件匹配正则（为空则全部）
            /// </summary>
            public string? FileRegex { get; set; }

            /// <summary>
            /// 进度报告
            /// </summary>
            public IProgress<int>? ProgressHandler { get; set; }
        }

        private static Regex? CreateSafeRegex(string? pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return null;

            try
            {
                return new Regex(pattern, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                // 正则表达式非法，返回一个永远不匹配的 Regex
                return new Regex("$a");
            }
        }

        /// <summary>
        /// 扫描所有路径，提取文件
        /// </summary>
        /// <param name="isRecursive">是否递归扫描子目录</param>
        public static async Task<List<string>> ScanPathsAsync(string[] paths, ScanOptions options)
        {
            return await Task.Run(() =>
            {
                var results = new List<string>();

                Regex? fileRegex = CreateSafeRegex(options.FileRegex);
                Regex? dirRegex = CreateSafeRegex(options.DirectoryRegex);

                int processed = 0;

                foreach (var path in paths)
                {
                    if (File.Exists(path))
                    {
                        TryAddFile(path, results, options, fileRegex);
                    }
                    else if (Directory.Exists(path))
                    {
                        TryAddDirectory(path, results, options, dirRegex);

                        ScanDirectory(path, results, options, fileRegex, dirRegex);
                    }

                    processed++;

                    if (paths.Length > 0)
                        options.ProgressHandler?.Report(processed * 100 / paths.Length);
                }

                return results.Distinct().ToList();
            });
        }

        /// <summary>
        /// 扫描目录
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="results"></param>
        /// <param name="options"></param>
        /// <param name="fileRegex"></param>
        /// <param name="dirRegex"></param>
        private static void ScanDirectory(string directory, List<string> results, ScanOptions options, Regex? fileRegex, Regex? dirRegex)
        {
            try
            {
                // 文件
                if (options.IncludeFiles)
                {
                    foreach (var file in Directory.GetFiles(directory))
                    {
                        TryAddFile(file, results, options, fileRegex);
                    }
                }

                // 子目录
                foreach (var dir in Directory.GetDirectories(directory))
                {
                    TryAddDirectory(dir, results, options, dirRegex);

                    if (options.IsRecursive)
                    {
                        ScanDirectory(dir, results, options, fileRegex, dirRegex);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 跳过无权限目录
            }
        }

        /// <summary>
        /// 文件匹配逻辑
        /// </summary>
        /// <param name="file"></param>
        /// <param name="results"></param>
        /// <param name="options"></param>
        /// <param name="fileRegex"></param>
        private static void TryAddFile(string file, List<string> results, ScanOptions options, Regex? fileRegex)
        {
            if (!options.IncludeFiles)
                return;

            if (fileRegex == null)
            {
                results.Add(file);
                return;
            }

            if (fileRegex.IsMatch(Path.GetFileName(file)))
            {
                results.Add(file);
            }
        }

        /// <summary>
        /// 目录匹配逻辑
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="results"></param>
        /// <param name="options"></param>
        /// <param name="dirRegex"></param>
        private static void TryAddDirectory(string dir, List<string> results, ScanOptions options, Regex? dirRegex)
        {
            if (!options.IncludeDirectories)
                return;

            if (dirRegex == null)
            {
                results.Add(dir);
                return;
            }

            if (dirRegex.IsMatch(Path.GetFileName(dir)))
            {
                results.Add(dir);
            }
        }

        /// <summary>
        /// 递归获取文件
        /// </summary>
        /// <param name="rootPath">根目录</param>
        /// <returns></returns>
        private static List<string> GetFilesRecursive(string rootPath)
        {
            var result = new List<string>();
            try
            {
                // 使用 TopDirectoryOnly 配合手动递归可以更好处理权限异常
                result.AddRange(Directory.GetFiles(rootPath));
                foreach (var directory in Directory.GetDirectories(rootPath))
                {
                    result.AddRange(GetFilesRecursive(directory));
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 跳过无权限访问的系统文件夹（如 System Volume Information）
            }
            return result;
        }


        // 配置 JSON 序列化选项（全局静态复用，提高性能）
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            // 解决中文乱码：默认会把中文转义为 Unicode 编码，这里强制不转义
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            // 格式化输出：让保存的 JSON 文件带有缩进，方便人类阅读
            WriteIndented = true,
            // 忽略大小写（反序列化时有用）
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// 将对象异步保存为 JSON 文件
        /// </summary>
        /// <typeparam name="T">要保存的数据类型</typeparam>
        /// <param name="filePath">保存路径</param>
        /// <param name="data">数据对象</param>
        /// <returns>是否保存成功</returns>
        public static async Task<bool> SaveAsJsonAsync<T>(string filePath, T data)
        {
            try
            {
                // 确保目标文件夹存在
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 异步序列化并写入文件
                using FileStream createStream = File.Create(filePath);
                await JsonSerializer.SerializeAsync(createStream, data, _jsonOptions);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveAsJsonAsync 出错 ：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 将文本内容异步保存到指定文件
        /// </summary>
        /// <param name="filePath">完整的文件路径 (包含扩展名，如 .json 或 .txt)</param>
        /// <param name="content">要保存的文本字符串</param>
        /// <param name="encoding">字符编码，默认使用 UTF8 (带 BOM，对中文支持最好)</param>
        /// <returns>是否保存成功</returns>
        public static async Task<bool> SaveTextAsync(string filePath, string content, Encoding? encoding = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath)) return false;

                // 确保目标目录存在
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 写入文本（如果文件已存在，则覆盖）
                // 使用异步写入，防止大文本保存时阻塞 UI 线程
                await File.WriteAllTextAsync(filePath, content, encoding ?? Encoding.UTF8);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveTextAsync 执行出错：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取指定目录下指定扩展名的文件列表（不含子目录）
        /// </summary>
        /// <param name="directoryPath">目录路径</param>
        /// <param name="extension">扩展名（例如 ".json" 或 "json"）</param>
        /// <returns>文件的完整路径列表</returns>
        public static async Task<List<string>> GetFilesByExtensionAsync(string directoryPath, string extension)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(directoryPath))
                    {
                        return new List<string>();
                    }

                    // 确保扩展名格式统一（以 . 开头）
                    string searchPattern = extension.StartsWith(".") ? $"*{extension}" : $"*.{extension}";

                    // TopDirectoryOnly 确保不含子目录
                    return Directory.GetFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly).ToList();
                }
                catch (Exception)
                {
                    // 可以在这里记录日志
                    return new List<string>();
                }
            });
        }

        /// <summary>
        /// 异步移动文件
        /// </summary>
        /// <param name="source">源路径</param>
        /// <param name="target">目标路径</param>
        /// <returns>返回 true 表示操作成功，false 表示失败（如目标已存在或发生异常）</returns>
        public static async Task<bool> MoveFileAsync(string source, string target)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 1. 基础校验
                    if (!File.Exists(source)) return false;
                    if (File.Exists(target)) return false;

                    // 2. 执行移动
                    File.Move(source, target);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    return false;
                }
            });
        }

        /// <summary>
        /// 异步删除文件
        /// </summary>
        /// <param name="path">文件完整路径</param>
        /// <returns>返回 true 表示删除成功或文件本身就不存在；false 表示删除失败（如权限不足、文件被占用）</returns>
        public static async Task<bool> DeleteFileAsync(string path)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 1. 如果文件本身就不存在，直接返回 true（符合“删除”后的预期状态）
                    if (!File.Exists(path)) return true;

                    // 2. 执行删除
                    File.Delete(path);
                    return true;
                }
                catch (Exception ex)
                {
                    // 这里可以捕获具体的 IOException (文件被占用) 或 UnauthorizedAccessException (无权限)
                    Debug.WriteLine($"删除失败: {path}, 错误: {ex.Message}");
                    return false;
                }
            });
        }
    }
}

