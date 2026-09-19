using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Common.GlobalHelper
{
    public static class JsonOperationHelper
    {
        /// <summary>
        /// 安全反序列化ExperimentModel，自动兜底空成员
        /// </summary>
        /// <param name="fileJson">json字符串</param>
        /// <returns>失败返回null，内部字段不会为null</returns>
        public static T? DeserializeObejct<T>(string fileJson)
        {
            if (string.IsNullOrWhiteSpace(fileJson))
                return default;
            try
            {
                var model = JsonConvert.DeserializeObject<T>(fileJson);
                if (model == null) return default;
                return model;
            }
            catch (JsonException)
            {
                return default;
            }
        }
    }
}
