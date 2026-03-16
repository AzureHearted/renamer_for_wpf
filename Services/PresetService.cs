using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReNamer.Models.ReName;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ReNamer.Services
{
    public static class PresetService
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string x, string y);

        /// <summary>
        /// 根据预设目录和名称加载单个预设
        /// </summary>
        public static async Task<Preset?> GetPresetByNameAsync(string presetDir, string presetName)
        {
            var filePath = Path.Combine(presetDir, $"{presetName}.json");
            return await GetPresetByPathAsync(filePath);
        }

        /// <summary>
        /// 核心：将单个 JSON 文件解析为 Preset 对象
        /// </summary>
        public static async Task<Preset?> GetPresetByPathAsync(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                var jo = JObject.Parse(json);

                var preset = new Preset
                {
                    Name = jo["Name"]?.ToString() ?? Path.GetFileNameWithoutExtension(filePath),
                    Filters = new Preset.FilterConfig
                    {
                        File = new()
                        {
                            Enable = Convert.ToBoolean(jo["Filters"]?["File"]?["Enable"] ?? true),
                            Regex = (jo["Filters"]?["File"]?["Regex"] ?? "").ToString()
                        },
                        Folder = new()
                        {
                            Enable = Convert.ToBoolean(jo["Filters"]?["Folder"]?["Enable"] ?? true),
                            Regex = (jo["Filters"]?["Folder"]?["Regex"] ?? "").ToString()
                        }
                    }
                };

                if (jo["Rules"] is JArray rulesArray)
                {
                    foreach (var ruleItem in rulesArray)
                    {
                        // 1. 识别类型
                        var type = ruleItem["Type"]?.ToObject<RuleType>() ?? RuleType.Insert;

                        // 2. 实例化具体子类
                        BaseRule? concreteRule = type switch
                        {
                            RuleType.Insert => ruleItem.ToObject<InsertRule>(),
                            RuleType.Replace => ruleItem.ToObject<ReplaceRule>(),
                            RuleType.Remove => ruleItem.ToObject<RemoveRule>(),
                            RuleType.Serialize => ruleItem.ToObject<SerializeRule>(),
                            RuleType.Fill => ruleItem.ToObject<FillRule>(),
                            RuleType.Regex => ruleItem.ToObject<RegexRule>(),
                            RuleType.Extension => ruleItem.ToObject<ExtensionRule>(),
                            _ => null
                        };

                        if (concreteRule != null)
                        {
                            concreteRule.IsSilenced = true;

                            // 3. 配置填充设置（解决大小写或属性匹配问题）
                            var settings = new JsonSerializerSettings
                            {
                                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                                ObjectCreationHandling = ObjectCreationHandling.Replace
                            };
                            var serializer = JsonSerializer.Create(settings);

                            // 4. 执行数据灌入
                            using (var reader = ruleItem.CreateReader())
                            {
                                serializer.Populate(reader, concreteRule);
                            }

                            concreteRule.IsSilenced = false;
                            preset.Rules.Add(concreteRule);
                        }
                    }
                }


                return preset;
            }
            catch (Exception ex)
            {
                // 这里可以记录日志，防止一个预设文件损坏导致整个列表崩了
                return null;
            }
        }

        /// <summary>
        /// 加载所有预设
        /// </summary>
        public static async Task<List<Preset>> GetAllPresetsAsync(string presetDir)
        {
            if (string.IsNullOrWhiteSpace(presetDir) || !Directory.Exists(presetDir))
                return new List<Preset>();

            var filePaths = await FileService.GetFilesByExtensionAsync(presetDir, "json");
            var tasks = filePaths.Select(path => GetPresetByPathAsync(path));

            // 并行处理所有文件的解析，速度更快
            var results = await Task.WhenAll(tasks);

            // 过滤掉解析失败的 (null) 并排序
            return results.Where(p => p != null)
                          .Cast<Preset>()
                          .OrderBy(p => p.Name, new NaturalStringComparer())
                          .ToList();
        }

        // 自然排序比较器
        private class NaturalStringComparer : IComparer<string>
        {
            public int Compare(string? x, string? y) => StrCmpLogicalW(x ?? "", y ?? "");
        }

        /// <summary>
        /// 删除预设
        /// </summary>
        /// <param name="presetDir">预设文件所在的文件夹路径</param>
        /// <param name="presetName">预设名称</param>
        /// <returns></returns>
        public static async Task<bool> RemovePreset(string presetDir, string presetName)
        {
            var target = Path.Combine(presetDir, $"{presetName}.json");
            if (File.Exists(target))
            {
                try
                {
                    File.Delete(target);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
    }
}
