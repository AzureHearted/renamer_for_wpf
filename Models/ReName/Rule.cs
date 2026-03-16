using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PropertyChanged;
using ReNamer.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ReNamer.Models.ReName
{

    public enum RuleType
    {
        [Description("插入")]
        Insert,
        [Description("替换")]
        Replace,
        [Description("移除")]
        Remove,
        [Description("序列化")]
        Serialize,
        [Description("填充")]
        Fill,
        [Description("正则")]
        Regex,
        [Description("扩展名")]
        Extension
    }

    /// <summary>
    /// 基础规则类
    /// </summary>
    public abstract partial class BaseRule : ObservableObject
    {
        [ObservableProperty]
        private bool _enable = true;

        /// <summary>
        /// 是否被选中
        /// </summary>
        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private RuleType _type;

        public string TypeName => EnumExtensions.GetDescription(Type);

        [ObservableProperty]
        private bool _ignoreExt = true;

        // 抽象属性保持原样
        public abstract string Description { get; }


        /// <summary>
        /// 构造
        /// </summary>
        /// <param name="type">规则类型</param>
        /// <param name="isSilenced">创建后的初始静默状态</param>
        public BaseRule(RuleType type)
        {
            Type = type;
        }

        internal bool IsSilenced = true;

        // 克隆方法
        public BaseRule Clone()
        {
            // 1. 将当前对象序列化为 JObject (中间层)
            JObject jo = JObject.FromObject(this);

            jo["IsSilenced"] = true;
            jo["IsSelected"] = false;

            // 3. 将修改后的 JObject 转回具体的子类对象
            var clone = jo.ToObject(this.GetType()) as BaseRule;

            // 4. 手动恢复状态
            if (clone != null) clone.IsSilenced = false;

            return clone ?? throw new Exception("Clone failed");
        }

    }

    /// <summary>
    /// 插入位置枚举
    /// </summary>

    public enum InsertPosition
    {
        [Description("前缀")]
        Prefix,
        [Description("后缀")]
        Suffix,
        [Description("指定位置")]
        Index,
        [Description("字符之前")]
        Before,
        [Description("字符之后")]
        After,
        [Description("替换文件名")]
        Replace
    }

    /// <summary>
    /// 匹配范围枚举
    /// </summary>
    public enum MatchRange
    {
        [Description("全部")]
        All,
        [Description("首个")]
        First,
        [Description("末个")]
        Last
    }

    /// <summary>
    /// 插入规则
    /// </summary>
    public partial class InsertRule : BaseRule
    {
        [ObservableProperty]
        private string _content = String.Empty;

        [ObservableProperty]
        private InsertPosition _position;

        [ObservableProperty]
        private int _anchorIndex = 1;

        [ObservableProperty]
        private bool _reverseIndex;

        [ObservableProperty]
        private string _beforeAnchorText = String.Empty;

        [ObservableProperty]
        private string _afterAnchorText = String.Empty;

        [ObservableProperty]
        private bool _ignoreCase;

        [ObservableProperty]
        private bool _isExactMatch;

        partial void OnAnchorIndexChanged(int oldValue, int newValue)
        {
            if (IsSilenced || oldValue == newValue) return;
            Position = InsertPosition.Index;

        }

        partial void OnReverseIndexChanged(bool oldValue, bool newValue)
        {
            if (IsSilenced || oldValue == newValue) return;
            Position = InsertPosition.Index;
        }


        partial void OnBeforeAnchorTextChanged(string? oldValue, string newValue)
        {
            if (IsSilenced || oldValue == newValue) return;
            Position = InsertPosition.Before;
        }

        partial void OnAfterAnchorTextChanged(string? oldValue, string newValue)
        {
            if (IsSilenced || oldValue == newValue) return;
            Position = InsertPosition.After;
        }

        public InsertRule() : base(RuleType.Insert)
        {

        }


        public override string Description
        {
            get
            {
                string desc = $"插入 \"{Content}\" ";
                if (Position == InsertPosition.Prefix || Position == InsertPosition.Suffix)
                {
                    var direction = Position == InsertPosition.Prefix ? "前" : "后";
                    desc += $"作为{direction}缀";
                }

                if (Position == InsertPosition.Index)
                {
                    desc += $"在位置 {AnchorIndex} 处";
                    if (ReverseIndex)
                    {
                        desc += "（从右到左）";

                    }
                }

                if (Position == InsertPosition.Before || Position == InsertPosition.After)
                {
                    desc += $"在 \"{(Position == InsertPosition.Before ? BeforeAnchorText : AfterAnchorText)}\"";

                    desc += IgnoreCase ? "（不区分大小写）" : "";

                    desc += Position == InsertPosition.Before ? "之前" : "之后";
                }

                if (Position == InsertPosition.Replace)
                {
                    desc += "替换当前文件名";
                }

                if (IgnoreExt)
                {
                    desc += "（忽略扩展名）";
                }

                return desc;
            }
        }

    }

    /// <summary>
    /// 替换规则
    /// </summary>
    public partial class ReplaceRule : BaseRule
    {
        [ObservableProperty]
        private string _match = String.Empty;

        [ObservableProperty]
        private string _replaceTo = String.Empty;

        [ObservableProperty]
        private MatchRange _range = MatchRange.All;

        [ObservableProperty]
        private bool _ignoreCase;

        [ObservableProperty]
        private bool _isExactMatch;

        public ReplaceRule() : base(RuleType.Replace)
        {
        }

        public override string Description
        {
            get
            {
                var rangeMap = new Dictionary<MatchRange, string>
                    {
                        { MatchRange.All, "全部" },
                        { MatchRange.First, "首个" },
                        { MatchRange.Last, "末个" }
                    };


                // 获取当前 Range 对应的中文，如果找不到则显示原名
                string rangeStr = EnumExtensions.GetDescription(Range);

                string desc = $"将{rangeStr} \"{Match}\" 替换为 \"{ReplaceTo}\"";

                if (IgnoreExt)
                {
                    desc += "（忽略扩展名）";
                }

                if (IgnoreCase)
                {
                    desc += "（不区分大小写）";
                }

                return desc;
            }
        }
    }

    /// <summary>
    /// 移除规则
    /// </summary>
    public partial class RemoveRule : BaseRule
    {
        [ObservableProperty]
        private string _match = String.Empty;

        [ObservableProperty]
        private MatchRange _range = MatchRange.All;

        [ObservableProperty]
        private bool _ignoreCase;

        [ObservableProperty]
        private bool _isExactMatch;

        public RemoveRule() : base(RuleType.Remove)
        {

        }

        public override string Description
        {
            get
            {
                string desc = $"移除 {EnumExtensions.GetDescription(Range)} \"{Match}\"";

                if (IgnoreExt)
                {
                    desc += "（忽略扩展名）";
                }

                if (IgnoreCase)
                {
                    desc += "（不区分大小写）";
                }

                return desc;
            }
        }
    }

    /// <summary>
    /// 序列化规则
    /// </summary>
    public partial class SerializeRule : BaseRule
    {
        [ObservableProperty]
        private InsertPosition _position = InsertPosition.Prefix;

        [ObservableProperty]
        private int _anchorIndex = 1;

        [ObservableProperty]
        private bool _reverseIndex;

        [ObservableProperty]
        private string _beforeAnchorText = String.Empty;

        [ObservableProperty]
        private string _afterAnchorText = String.Empty;

        /// <summary>
        /// 序列起始值
        /// </summary>
        [ObservableProperty]
        private int _sequenceStart = 1;

        /// <summary>
        /// 序列步长
        /// </summary>
        [ObservableProperty]
        private int _sequenceStep = 1;

        /// <summary>
        /// 补零数量 (-1:自动填充 0：不进行补零 >0:填充指定数量的0) 
        /// </summary>
        [ObservableProperty]
        private int _paddingCount = -1;

        /// <summary>
        /// 文件夹变更重置
        /// </summary>
        [ObservableProperty]
        private bool _resetFolderChanges = true;

        [ObservableProperty]
        private bool _ignoreCase;

        [ObservableProperty]
        private bool _isExactMatch;

        partial void OnAnchorIndexChanged(int oldValue, int newValue)
        {
            if (IsSilenced || oldValue == newValue) return;
            Position = InsertPosition.Index;

        }

        partial void OnReverseIndexChanged(bool oldValue, bool newValue)
        {
            if (IsSilenced || oldValue == newValue) return;
            Position = InsertPosition.Index;
        }


        partial void OnBeforeAnchorTextChanged(string? oldValue, string newValue)
        {
            if (IsSilenced || oldValue == newValue) return;
            Position = InsertPosition.Before;
        }

        partial void OnAfterAnchorTextChanged(string? oldValue, string newValue)
        {
            if (IsSilenced || oldValue == newValue) return;
            Position = InsertPosition.After;
        }

        public SerializeRule() : base(RuleType.Serialize)
        {

        }

        public override string Description
        {
            get
            {
                string desc = $"序列起始于 {SequenceStart} 增量 {SequenceStep}";

                if (ResetFolderChanges)
                {
                    desc += "（文件夹变更时重置）";
                }

                if (PaddingCount > 0)
                {
                    desc += $" 补足长度为 {PaddingCount} 位";
                }
                else if (PaddingCount <= -1)
                {
                    desc += " 并自动补足长度";
                }

                if (Position == InsertPosition.Prefix || Position == InsertPosition.Suffix)
                {
                    string direction = Position == InsertPosition.Prefix ? "前" : "后";
                    desc += $" 作为{direction}缀";
                }

                if (Position == InsertPosition.Index)
                {
                    desc += $" 序列插入到 {AnchorIndex} 处";

                    if (ReverseIndex)
                    {
                        desc += "（位置索引从右到左）";
                    }
                }

                if (Position == InsertPosition.Before || Position == InsertPosition.After)
                {
                    desc += $" 序列插入到 \"{(Position == InsertPosition.Before ? BeforeAnchorText : AfterAnchorText)}\"";
                    desc += IgnoreCase ? "（不区分大小写）" : "";
                    desc += Position == InsertPosition.Before ? "之前" : "之后";
                }

                if (Position == InsertPosition.Replace)
                {
                    desc += " 替换当前文件名";
                }

                if (IgnoreExt)
                {
                    desc += "（忽略扩展名）";
                }

                return desc;
            }
        }
    }



    /// <summary>
    /// 填充方向
    /// </summary>
    public enum PaddingDirection
    {
        [Description("左")]
        Left,
        [Description("右")]
        Right
    }

    /// <summary>
    /// 补零配置类
    /// </summary>

    public partial class ZeroPaddingConfig : ObservableObject
    {
        [ObservableProperty]
        private bool _enable = false;

        [ObservableProperty]
        private int _length = 3;
    }

    /// <summary>
    /// 文本填充配置类
    /// </summary>
    public partial class TextPaddingConfig : ObservableObject
    {
        [ObservableProperty]
        private bool _enable = false;

        [ObservableProperty]
        private string _character = "";

        [ObservableProperty]
        private int _length = 3;

        [ObservableProperty]
        private PaddingDirection _direction = PaddingDirection.Left;
    }

    /// <summary>
    /// 填充规则
    /// </summary>
    public partial class FillRule : BaseRule
    {
        /// <summary>
        /// 补零配置
        /// </summary>
        [ObservableProperty]
        private ZeroPaddingConfig _zeroPadding = new();

        /// <summary>
        /// 移除补零
        /// </summary>
        [ObservableProperty]
        private bool _removeZeroPadding;


        // 联动逻辑：当主属性 RemoveZeroPadding 改变时
        partial void OnRemoveZeroPaddingChanged(bool oldValue, bool newValue)
        {
            if (IsSilenced || oldValue == newValue) return;
            if (newValue && ZeroPadding.Enable) ZeroPadding.Enable = false;
        }

        /// <summary>
        /// 文本填充配置
        /// </summary>
        [ObservableProperty]
        private TextPaddingConfig _textPadding = new();


        public FillRule() : base(RuleType.Fill)
        {

            // 监听ZeroPadding的属性变化
            ZeroPadding.PropertyChanged += (s, e) =>
            {
                if (IsSilenced) return;
                // 监听 ZeroPadding.Enable 属性
                if (e.PropertyName == nameof(ZeroPadding.Enable))
                {
                    // 互斥逻辑：如果开启了补零填充，则关闭“移除补零”
                    if (ZeroPadding.Enable && RemoveZeroPadding)
                    {
                        RemoveZeroPadding = false;
                    }
                }

                //    // 监听 ZeroPadding.Length 属性
                if (e.PropertyName == nameof(ZeroPadding.Length))
                    ZeroPadding.Enable = true;
            };

            TextPadding.PropertyChanged += (s, e) =>
            {
                if (IsSilenced) return;
                // 监听 TextPadding.Length 属性
                if (e.PropertyName == nameof(TextPadding.Length) || e.PropertyName == nameof(TextPadding.Character) || e.PropertyName == nameof(TextPadding.Direction))
                    TextPadding.Enable = true;
            };
        }

        public override string Description
        {
            get
            {
                string desc = "";

                if (RemoveZeroPadding)
                {
                    desc += "移除补零";
                }
                else if (ZeroPadding.Enable)
                {
                    desc += $"补零填充，长度 {ZeroPadding.Length}";
                }

                if (TextPadding.Enable)
                {
                    if (ZeroPadding.Enable || RemoveZeroPadding)
                        desc += "；";

                    desc +=
                        $"文本填充，填充内容 \"{TextPadding.Character}\" ，长度 {TextPadding.Length}，{(TextPadding.Direction == PaddingDirection.Left ? "左侧" : "右侧")}";
                }

                if (IgnoreExt)
                {
                    desc += "（忽略扩展名）";
                }

                return desc;
            }
        }
    }

    /// <summary>
    /// 正则规则
    /// </summary>
    public partial class RegexRule : BaseRule
    {
        [ObservableProperty]
        private string _regex = String.Empty;

        [ObservableProperty]
        private string _replaceTo = String.Empty;

        [ObservableProperty]
        private bool _ignoreCase;

        [ObservableProperty]
        private bool _isExactMatch;

        public RegexRule() : base(RuleType.Regex)
        {

        }

        public override string Description
        {
            get
            {
                string desc = $"替换表达式 \"{Regex}\" ，替换为 \"{ReplaceTo}\"";

                if (IgnoreCase)
                {
                    desc += "（不区分大小写）";
                }

                if (IgnoreExt)
                {
                    desc += "（忽略扩展名）";
                }

                return desc;
            }
        }
    }

    /// <summary>
    /// 扩展名规则
    /// </summary>
    public partial class ExtensionRule : BaseRule
    {
        [ObservableProperty]
        public string _extension = String.Empty;

        public ExtensionRule() : base(RuleType.Extension)
        {
            IgnoreExt = false;
        }

        public override string Description
        {
            get
            {
                if (String.IsNullOrWhiteSpace(Extension))
                    return "移除扩展名";

                string desc = $"修改扩展名为 \"{Extension}\"";

                if (IgnoreExt)
                {
                    desc += "（添加到原始文件名）";
                }

                return desc;
            }
        }

    }
}
