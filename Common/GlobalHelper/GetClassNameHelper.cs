using System;
using System.Collections.Generic;
using System.Text;

namespace Common.GlobalHelper
{
    public static class GetClassNameHelper
    {
        /// <summary>
        /// 获取类的简单名称（不含命名空间）
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <returns>类名</returns>
        public static string GetClassName<T>()
        {
            return typeof(T).Name;
        }

        /// <summary>
        /// 获取类的完整名称（命名空间 + 类名）
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <returns>完整类型名称</returns>
        public static string GetClassFullName<T>()
        {
            return typeof(T).FullName ?? string.Empty;
        }

        /// <summary>
        /// 根据对象实例获取类简单名称（实例可为null，返回类型名）
        /// </summary>
        /// <param name="obj">对象实例</param>
        /// <returns>类名，null对象返回空字符串</returns>
        public static string GetClassName(object? obj)
        {
            if (obj == null)
                return string.Empty;
            return obj.GetType().Name;
        }

        /// <summary>
        /// 根据对象实例获取类完整名称
        /// </summary>
        /// <param name="obj">对象实例</param>
        /// <returns>完整名称</returns>
        public static string GetClassFullName(object? obj)
        {
            if (obj == null)
                return string.Empty;
            return obj.GetType().FullName ?? string.Empty;
        }

        /// <summary>
        /// 根据Type获取简单类名
        /// </summary>
        /// <param name="type">类型对象</param>
        /// <returns>类名</returns>
        public static string GetClassName(Type? type)
        {
            if (type == null)
                return string.Empty;
            return type.Name;
        }

        /// <summary>
        /// 根据Type获取完整类名
        /// </summary>
        /// <param name="type">类型对象</param>
        /// <returns>完整名称</returns>
        public static string GetClassFullName(Type? type)
        {
            if (type == null)
                return string.Empty;
            return type.FullName ?? string.Empty;
        }
    }
}
