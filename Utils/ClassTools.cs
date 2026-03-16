using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ReNamer.Utils
{
    internal class ClassTools
    {
        /// <summary>
        /// 深拷贝对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="source">对象源</param>
        /// <returns></returns>
        public static T? DeepClone<T>(T source)
        {
            if (source == null) return default;

            // 配置：必须包含类型信息，这样反序列化抽象类时才知道找哪个子类
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All // 关键：记录完整的类型名
            };

            // 序列化时传入 source.GetType()，确保抓取到子类的真实属性
            string json = JsonConvert.SerializeObject(source, settings);

            // 反序列化
            return (T)JsonConvert.DeserializeObject(json, source.GetType(), settings)!;
        }
    }
}
