using System;
using System.Security.Cryptography;
using System.Text;

namespace Common.GlobalHelper
{
    /// <summary>
    /// 字符串通用工具类
    /// </summary>
    public static class StringOperationHelper
    {
        /// <summary>
        /// 标准Guid（带横杠）
        /// 示例：6574218b-8322-4475-8611-925542f81234
        /// </summary>
        public static string GetGuid()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 32位无横杠Guid
        /// 示例：6574218b832244758611925542f81234
        /// </summary>
        public static string GetGuidWithoutDash()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// 【年月日时分秒 + 无横杠Guid】默认下划线分隔
        /// 格式：yyyyMMddHHmmss_Guid32
        /// 示例：20260804162015_6574218b832244758611925542f81234
        /// </summary>
        public static string GetDateTimeWithGuid(string separator = "_")
        {
            var timePart = DateTime.Now.ToString("yyyyMMddHHmmss");
            var guidPart = GetGuidWithoutDash();
            return $"{timePart}{separator}{guidPart}";
        }

        /// <summary>
        /// 将任意字符串(支持中文、英文、数字、符号)映射为【固定长度大写英文字母字符串】
        /// 相同输入输出一定相同；不同输入碰撞概率极低；输出仅A‑Z大写字母
        /// </summary>
        /// <param name="serialString">原始字符串，可以中文</param>
        /// <param name="fixedLength">输出字母串长度，默认8</param>
        /// <returns>定长大写字母；输入null/空返回空字符串</returns>
        public static string StringCorrespondLetterString(string serialString, int fixedLength = 20)
        {
            if (string.IsNullOrEmpty(serialString))
                return string.Empty;

            using var sha256 = SHA256.Create();
            byte[] inputBytes = Encoding.UTF8.GetBytes(serialString);
            byte[] hashBytes = sha256.ComputeHash(inputBytes);

            char[] result = new char[fixedLength];
            for (int i = 0; i < fixedLength; i++)
            {
                byte b = hashBytes[i % hashBytes.Length];
                int offset = b % 26;
                result[i] = (char)('A' + offset);
            }
            return new string(result);
        }
    }
}