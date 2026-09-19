using Common.LocalEntity;

namespace Common.GlobalHelper;

/// <summary>
/// 参考文档：
/// 1 ASP.NET Core 工业自动化项目统一JSON规范方案
/// </summary>
public static class ApiReturnHelper
{
    // 成功无数据
    public static ApiUnifiedReturnStructure<object> Success(object? data = null, string msg = "操作成功")
        => new ApiUnifiedReturnStructure<object> { Code = 200, Msg = msg, Data = data! };

    // 成功带数据
    public static ApiUnifiedReturnStructure<T> Success<T>(T data, string msg = "操作成功")
        => new ApiUnifiedReturnStructure<T> { Code = 200, Msg = msg, Data = data };

    // 内部错误-无数据
    public static ApiUnifiedReturnStructure<object> ClientError(object? data = null,string msg= "客户端错误")
        => new ApiUnifiedReturnStructure<object> { Code = 400, Msg = msg, Data = data! };
    // 内部错误-带数据
    public static ApiUnifiedReturnStructure<T> ClientError<T>(T data,string msg= "客户端错误")
        => new ApiUnifiedReturnStructure<T> { Code = 400, Msg = msg, Data = data };

    // 内部错误-无数据
    public static ApiUnifiedReturnStructure<object> ServerError(object? data = null,string msg = "服务端错误")
        => new ApiUnifiedReturnStructure<object> { Code = 500, Msg = msg, Data = data! };
    // 内部错误-带数据
    public static ApiUnifiedReturnStructure<T> ServerError<T>(T data, string msg = "服务端错误")
        => new ApiUnifiedReturnStructure<T> { Code = 500, Msg = msg, Data = data };
}