using Common.GlobalHelper;
using Common.LocalEntity;
using ConnectionSocket;
using Microsoft.AspNetCore.Mvc;

namespace NetworkComponent.Controllers
{
    /// <summary>
    /// 通用 TCP Socket 操作控制器。
    /// 对外暴露二进制/字符串收发、纯发送、连通性测试等 HTTP 接口，适用于非标 TCP 设备。
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class SocketClientOperationController : ControllerBase
    {
        private readonly ISocketClient _socketClient;

        /// <summary>
        /// 构造函数注入 Socket 客户端服务。
        /// </summary>
        public SocketClientOperationController(ISocketClient socketClient)
        {
            _socketClient = socketClient;
        }

        /// <summary>
        /// 连接设备、发送十六进制字节并等待应答（短连接）。
        /// </summary>
        /// <param name="input">二进制收发入参</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<byte[]>> SendAndReceiveBytes([FromBody] SocketBinaryInput input)
        {
            var resp = await _socketClient.SendAndReceiveAsync(
                input.DeviceCode, input.Data ?? Array.Empty<byte>(), input.TimeoutMs);
            return ApiReturnHelper.Success(resp);
        }

        /// <summary>
        /// 按配置编码发送字符串并等待字符串应答。
        /// </summary>
        /// <param name="input">字符串收发入参</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<string>> SendAndReceiveString([FromBody] SocketStringInput input)
        {
            var resp = await _socketClient.SendAndReceiveStringAsync(
                input.DeviceCode, input.Message ?? string.Empty, input.TimeoutMs);
            return ApiReturnHelper.Success(resp);
        }

        /// <summary>
        /// 仅发送字节，不等待设备应答。
        /// </summary>
        /// <param name="input">纯发送入参</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> SendOnly([FromBody] SocketBinaryInput input)
        {
            await _socketClient.SendAsync(input.DeviceCode, input.Data ?? Array.Empty<byte>());
            return ApiReturnHelper.Success(null, "发送完成");
        }

        /// <summary>
        /// 测试与设备的 TCP 连通性。
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        [HttpGet]
        public async Task<ApiUnifiedReturnStructure<bool>> TestConnection(string deviceCode)
        {
            var ok = await _socketClient.TestConnectionAsync(deviceCode);
            return ok ? ApiReturnHelper.Success(true, "连接成功") : ApiReturnHelper.ServerError(false, "连接失败");
        }

        /// <summary>
        /// 获取全部已加载的 Socket 设备编码列表。
        /// </summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<List<string>> GetAllDeviceCode()
        {
            return ApiReturnHelper.Success(_socketClient.GetAllDeviceCodes());
        }
    }

    #region DTO

    /// <summary>二进制收发/纯发送入参</summary>
    public class SocketBinaryInput
    {
        /// <summary>设备编码</summary>
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary>待发送字节数组</summary>
        public byte[]? Data { get; set; }

        /// <summary>可选超时（毫秒），不传则用配置默认值</summary>
        public int? TimeoutMs { get; set; }
    }

    /// <summary>字符串收发入参</summary>
    public class SocketStringInput
    {
        /// <summary>设备编码</summary>
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary>待发送字符串</summary>
        public string? Message { get; set; }

        /// <summary>可选超时（毫秒）</summary>
        public int? TimeoutMs { get; set; }
    }

    #endregion
}
