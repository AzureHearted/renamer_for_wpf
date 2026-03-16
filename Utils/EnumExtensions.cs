using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ReNamer.Utils
{
    public static class EnumExtensions
    {
        /// <summary>
        /// 尝试获取枚举值对应的描述
        /// </summary>
        /// <param name="value">枚举值</param>
        /// <returns></returns>
        public static string GetDescription(this Enum value)
        {
            // 1. 获取枚举类型的 Type 信息
            Type type = value.GetType();

            // 2. 获取该枚举成员的字段信息
            FieldInfo? fieldInfo = type.GetField(value.ToString());

            // 3. 尝试获取字段上的 DescriptionAttribute 特性
            if (fieldInfo != null)
            {
                var attribute = fieldInfo.GetCustomAttribute<DescriptionAttribute>();
                if (attribute != null)
                {
                    return attribute.Description; // 返回 "插入"、"替换" 等
                }
            }

            // 4. 如果没有定义特性，则返回枚举自身的字符串名称 (例如 "Insert")
            return value.ToString();
        }
    }
}
