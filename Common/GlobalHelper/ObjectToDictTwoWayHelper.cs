using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Common.GlobalHelper
{
    public static class ObjectToDictTwoWayHelper
    {
        /// <summary>
        /// 对象转 Dictionary&lt;string, string&gt;  key:属性名，value:值ToString()
        /// </summary>
        public static Dictionary<string, string> ToDictString(this object obj)
        {
            var dict = new Dictionary<string, string>();
            if (obj == null) return dict;

            PropertyInfo[] props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (!prop.CanRead) continue;

                object? val = prop.GetValue(obj);
                string strVal = val == null ? string.Empty : (val.ToString() ?? string.Empty);
                dict.Add(prop.Name, strVal);
            }
            return dict;
        }

        /// <summary>
        /// Dictionary&lt;string,string&gt; 反射映射为实体对象T
        /// </summary>
        /// <typeparam name="T">目标实体，必须有无参构造函数</typeparam>
        /// <param name="dict">源字典 key=属性名 value=字符串值</param>
        /// <returns>填充后的T实例</returns>
        public static T ToModel<T>(this Dictionary<string, string> dict) where T : new()
        {
            if (dict == null)
                return new T();

            T model = new T();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                // 只处理可写属性
                if (!prop.CanWrite)
                    continue;

                // 字典中不存在该属性，跳过
                if (!dict.TryGetValue(prop.Name, out string? strValue))
                    continue;

                // 空字符串，值类型跳过，引用类型设null
                if (string.IsNullOrEmpty(strValue))
                {
                    if (!prop.PropertyType.IsValueType)
                    {
                        prop.SetValue(model, null);
                    }
                    continue;
                }

                object convertedValue;
                Type targetType = prop.PropertyType;

                try
                {
                    // 数值类型用不变文化，避免小数点逗号问题
                    if (targetType == typeof(double) || targetType == typeof(float) || targetType == typeof(decimal))
                    {
                        convertedValue = Convert.ChangeType(strValue, targetType, CultureInfo.InvariantCulture);
                    }
                    else if (targetType == typeof(bool))
                    {
                        // 兼容 "1"/"0" "true"/"false"
                        convertedValue = bool.Parse(strValue);
                    }
                    else
                    {
                        convertedValue = Convert.ChangeType(strValue, targetType, CultureInfo.InvariantCulture);
                    }

                    prop.SetValue(model, convertedValue);
                }
                catch
                {
                    // 转换失败：忽略，保留属性默认值；业务层可根据需要抛异常
                }
            }

            return model;
        }
    }
}
