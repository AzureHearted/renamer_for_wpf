using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ReNamer.Models.ReName
{
    /// <summary>
    /// 重命名预设类
    /// </summary>
    public partial class Preset : ObservableObject
    {

        /// <summary>
        /// 预设名称
        /// </summary>
        [ObservableProperty]
        private string _name = String.Empty;

        /// <summary>
        /// 预设规则列表
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<BaseRule> _rules = [];

        /// <summary>
        /// 过滤配置（文件和文件夹）
        /// </summary>
        [ObservableProperty]
        private FilterConfig _filters = new();


        /// <summary>
        /// 顶级过滤器配置
        /// </summary>
        public partial class FilterConfig : ObservableObject
        {
            [ObservableProperty]
            private FilterItem _file = new() { Enable = true };

            [ObservableProperty]
            private FilterItem _folder = new() { Enable = false };
        }

        /// <summary>
        /// 具体的过滤项
        /// </summary>
        public partial class FilterItem : ObservableObject
        {
            /// <summary>
            /// 使用 int 兼容你 JSON 中的 0 和 1，如果 UI 绑定 CheckBox，建议后续转为 bool
            /// </summary>
            [ObservableProperty]
            private bool _enable;

            [ObservableProperty]
            private string _regex = string.Empty;

            partial void OnRegexChanged(string? oldValue, string newValue)
            {
                if (oldValue?.Trim() == newValue.Trim()) return;
                this._regex = newValue.Trim();
                if (!String.IsNullOrWhiteSpace(this._regex))
                    Enable = true;
            }
        }

        /// <summary>
        /// 将当前预设对象转换为 JSON 字符串
        /// </summary>
        /// <returns>JSON 文本</returns>
        public string ToJson()
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented, // 缩进，美观
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore, // 防止循环引用
                                                                          // 确保所有的枚举、子类属性都能被正确识别
                };

                // 核心改动：用 JsonConvert 而不是 JsonSerializer
                return JsonConvert.SerializeObject(this, settings);
            }
            catch (Exception ex)
            {
                return $"{{ \"error\": \"{ex.Message}\" }}";
            }
        }

        // 克隆方法
        public Preset Clone()
        {
            // 配置：在 JSON 中包含类型信息，这样反序列化就知道谁是谁了
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            // 1. 序列化（带上类型“身份证”）
            string json = JsonConvert.SerializeObject(this, settings);

            // 2. 反序列化（根据“身份证”找回具体的子类）
            var clone = JsonConvert.DeserializeObject<Preset>(json, settings);

            return clone ?? throw new Exception("Clone failed");
        }

    }
}
