using System;
using System.Collections.Generic;
using System.Text;

namespace Common.GlobalHelper
{
    public static class GetEnumNameHelper
    {
        /// <summary>
        /// 根据枚举值，返回枚举名称字符串
        /// </summary>
        /// <typeparam name="T">枚举类型</typeparam>
        /// <param name="currentValue">当前传入枚举值</param>
        /// <returns>枚举名称字符串</returns>
        public static string GetEnumName<T>(T currentValue)
            where T : Enum
        {
            return Enum.GetName(typeof(T), currentValue) ?? string.Empty;
        }

        /// <summary>
        /// 获取枚举所有项的名称字符串列表
        /// </summary>
        /// <typeparam name="T">枚举类型</typeparam>
        /// <returns>枚举名称集合</returns>
        public static List<string> GetEnumNameList<T>() where T : Enum
        {
            // 获取全部枚举名称，转为List<string>
            return Enum.GetNames(typeof(T)).ToList();
        }

        /// <summary>
        /// 获取枚举【值+名称】键值对列表
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>(int Value, string Name)</returns>
        public static List<(int Value, string Name)> GetEnumValueNameList<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .Select(x => ((int)(object)x, x.ToString()))
                .ToList();
        }
    }
}
