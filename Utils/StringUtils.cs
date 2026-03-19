using ReNamer.Models.ReName;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ReNamer.Utils
{
    /// <summary>
    /// 字符串工具
    /// </summary>
    public class StringUtils
    {
        /// <summary>
        /// 插入内容到指定索引
        /// </summary>
        /// <param name="text">要处理的字符串</param>
        /// <param name="insert">要插入的内容</param>
        /// <param name="index">插入位置</param>
        /// <param name="reverse">反转索引位置</param>
        /// <returns></returns>
        public static string InsertByIndex(string text, string insert, int index, bool reverse)
        {
            if (text == null) return insert;
            int strLength = text.Length;
            int insertPos;

            if (!reverse)
            {
                // 从左向右：index 0 是最左侧
                insertPos = index;
            }
            else
            {
                // 从右向左：index 0 是最右侧（末尾）
                insertPos = strLength - index;
            }

            // 确保位置不会小于 0 且不会超过字符串当前长度
            insertPos = Math.Clamp(insertPos, 0, strLength);

            return text.Insert(insertPos, insert);
        }

        /// <summary>
        /// 基于匹配结果插入字符串
        /// </summary>
        /// <param name="original">原始字符串</param>
        /// <param name="insert">要插入的内容</param>
        /// <param name="match">要匹配的字面量字符串</param>
        /// <param name="position">插入位置</param>
        /// <param name="ignoreCase">是否忽略大小写</param>
        /// <param name="isExactMatch">是否全字匹配</param>
        /// <returns></returns>
        public static string InsertByMatch(
            string original,
            string insert,
            string match,
            InsertPosition position,
            bool ignoreCase = false,
            bool isExactMatch = false)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(match))
                return original;

            // 构建正则表达式
            string escapedMatch = Regex.Escape(match); // 转义正则特殊字符
            string pattern = isExactMatch ? $@"\b({escapedMatch})\b" : $"({escapedMatch})";

            // 设置正则选项
            RegexOptions options = RegexOptions.None;
            if (ignoreCase)
            {
                options |= RegexOptions.IgnoreCase;
            }

            // 构建替换字符串

            // 在 C# Regex.Replace 中：
            // * $1 表示第一个捕获组
            // * $$ 表示字面量 $ 符号
            string safeInsert = insert.Replace("$", "$$");

            string replacement;
            if (position == InsertPosition.Before)
            {
                // 插入内容 + 原匹配内容 ($1)
                replacement = safeInsert + "${1}";
            }
            else
            {
                // 原匹配内容 ($1) + 插入内容
                replacement = "${1}" + safeInsert;
            }

            // 执行替换
            return Regex.Replace(original, pattern, replacement, options);
        }

        /// <summary>
        /// 替换字符串中匹配的内容
        /// </summary>
        /// <param name="original">原字符串</param>
        /// <param name="match">匹配内容</param>
        /// <param name="replaceTo">替换为</param>
        /// <param name="range">匹配范围</param>
        /// <param name="ignoreCase">忽略大小写</param>
        /// <param name="isExactMatch">全字匹配</param>
        /// <returns></returns>
        public static string Replace(string original, string match, string replaceTo, MatchRange range = MatchRange.All, bool ignoreCase = false, bool isExactMatch = false)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(match))
                return original;

            // 构建正则表达式 Pattern
            string escapedMatch = Regex.Escape(match);
            string pattern = isExactMatch ? $@"\b{escapedMatch}\b" : escapedMatch;

            // 配置正则选项
            RegexOptions options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
            Regex regex = new Regex(pattern, options);

            // 处理替换逻辑
            switch (range)
            {
                case MatchRange.First:
                    return regex.Replace(original, replaceTo, 1); // C# Regex.Replace 接受 count 参数，1 表示只替换第一个

                case MatchRange.Last:
                    // 查找所有匹配项
                    var matches = regex.Matches(original);
                    if (matches.Count == 0) return original;

                    // 获取最后一个匹配项
                    Match lastMatch = matches[matches.Count - 1];

                    // 手动拼接：前半部分 + 替换值 + 后半部分
                    return original.Remove(lastMatch.Index, lastMatch.Length).Insert(lastMatch.Index, replaceTo);
                case MatchRange.All:
                default:
                    return regex.Replace(original, replaceTo);
            }
        }

        /// <summary>
        /// 移除字符串中匹配的内容
        /// </summary>
        /// <param name="original">原字符串</param>
        /// <param name="match">匹配内容</param>
        /// <param name="range">匹配范围</param>
        /// <param name="ignoreCase">忽略大小写</param>
        /// <param name="isExactMatch">全字匹配</param>
        /// <returns></returns>
        public static string Remove(string original, string match, MatchRange range = MatchRange.All, bool ignoreCase = false, bool isExactMatch = false)
        {
            return Replace(original, match, "", range, ignoreCase, isExactMatch);
        }
    }
}
