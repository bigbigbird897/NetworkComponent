using Common.GlobalHelper;
using Common.LocalEntity;
using ConnectionOPCUA;
using Microsoft.AspNetCore.Mvc;

namespace NetworkComponent.Controllers
{
    /// <summary>
    /// OPC UA 节点读写操作控制器。
    /// 对外暴露 OPC UA 服务端节点的读、写、批量读、连接测试等 HTTP 接口。
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class OpcUaOperationController : ControllerBase
    {
        private readonly IOpcUaClient _opcUaClient;

        /// <summary>
        /// 构造函数注入 OPC UA 客户端服务。
        /// </summary>
        public OpcUaOperationController(IOpcUaClient opcUaClient)
        {
            _opcUaClient = opcUaClient;
        }

        /// <summary>
        /// 读取单个 OPC UA 节点当前值。
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="nodeId">节点ID，如 ns=2;s=Temperature</param>
        [HttpGet]
        public async Task<ApiUnifiedReturnStructure<object?>> ReadNode(string deviceCode, string nodeId)
        {
            var data = await _opcUaClient.ReadNodeAsync(deviceCode, nodeId);
            return ApiReturnHelper.Success<object?>(data);
        }

        /// <summary>
        /// 批量读取多个 OPC UA 节点。
        /// </summary>
        /// <param name="input">批量读取入参（设备编码 + 节点ID列表）</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<Dictionary<string, object?>>> ReadNodes([FromBody] OpcUaReadNodesInput input)
        {
            var data = await _opcUaClient.ReadNodesAsync(input.DeviceCode, input.NodeIds ?? new List<string>());
            return ApiReturnHelper.Success(data);
        }

        /// <summary>
        /// 向单个 OPC UA 节点写入值。
        /// </summary>
        /// <param name="input">写入入参（设备编码 + 节点ID + 值）</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> WriteNode([FromBody] OpcUaWriteNodeInput input)
        {
            await _opcUaClient.WriteNodeAsync(input.DeviceCode, input.NodeId, input.Value ?? string.Empty, input.DataType);
            return ApiReturnHelper.Success(null, "写入完成");
        }

        /// <summary>
        /// 测试与指定 OPC UA 服务端的连通性。
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        [HttpGet]
        public async Task<ApiUnifiedReturnStructure<bool>> TestConnection(string deviceCode)
        {
            var ok = await _opcUaClient.TestConnectionAsync(deviceCode);
            return ok ? ApiReturnHelper.Success(true, "连接成功") : ApiReturnHelper.ServerError(false, "连接失败");
        }

        /// <summary>
        /// 获取全部已加载的 OPC UA 设备编码列表。
        /// </summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<List<string>> GetAllDeviceCode()
        {
            return ApiReturnHelper.Success(_opcUaClient.GetAllDeviceCodes());
        }
        /// <summary>
        /// 鎵归噺鏌ヨ鎵€鏈?OPC UA 璁惧鐨勮繛鎺ョ姸鎬侊紙閫愪釜 TestConnection 鎺㈡祴锛夈€?        /// </summary>
        [HttpGet]
        public async Task<ApiUnifiedReturnStructure<Dictionary<string, bool>>> GetAllDeviceStatus()
        {
            return ApiReturnHelper.Success(await _opcUaClient.GetAllDeviceStatusAsync());
        }
    }

    #region DTO

    /// <summary>批量读取节点入参</summary>
    public class OpcUaReadNodesInput
    {
        /// <summary>设备编码</summary>
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary>待读取节点ID列表</summary>
        public List<string>? NodeIds { get; set; }
    }

    /// <summary>写入节点入参</summary>
    public class OpcUaWriteNodeInput
    {
        /// <summary>设备编码</summary>
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary>节点ID</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>待写入值（字符串形式，后端按 DataType 转换）</summary>
        public object? Value { get; set; }

        /// <summary>数据类型：Boolean/SByte/Int16/UInt16/Int32/UInt32/Int64/UInt64/Float/Double/String</summary>
        public string DataType { get; set; } = "Int32";
    }

    #endregion
}
