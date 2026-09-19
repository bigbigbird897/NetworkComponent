using System;
using System.Collections.Generic;
using System.Text;

namespace Common.GlobalHelper
{
    public class ClassOperationHelper
    {
        // 简单思路，可封装成工具方法
        public static bool DeepValueEqual<T>(T a, T b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.GetType() != b.GetType()) return false;

            var props = typeof(T).GetProperties();
            foreach (var p in props)
            {
                var v1 = p.GetValue(a);
                var v2 = p.GetValue(b);
                if (!Equals(v1, v2))
                    return false;
            }
            return true;
        }

    }
}
