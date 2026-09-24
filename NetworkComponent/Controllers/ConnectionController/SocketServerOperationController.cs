using Common.GlobalHelper;
using Common.LocalEntity;
using ConnectionSocket;
using ConnectionSocket.LocalEntity;
using Microsoft.AspNetCore.Mvc;

namespace NetworkComponent.Controllers
{
    /// <summary>
    /// TCP Socket 服务端（监听端）操作控制器。
    /// 用于启动/停止监听、向指定或全部已连接客户端发送数据、查看在线客户端与最近收到的消息。
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class SocketServerOperationController : ControllerBase
    {
        private readonly ISocketServerService _server;

        /// <summary>
        /// 构造函数注入 Socket 服务端服务。
        /// </summary>
        public SocketServerOperationController(ISocketServerService server)
        {
            _server = server;
        }

        /// <summary>
        /// 启动指定服务端（开始监听）。
        /// </summary>
        /// <param name="serverCode">服务端编码</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> StartServer(string serverCode)
        {
            await _server.StartAsync(serverCode);
            return ApiReturnHelper.Success(null, $"服务端 {serverCode} 已启动");
        }

        /// <summary>
        /// 停止指定服务端并断开全部客户端。
        /// </summary>
        /// <param name="serverCode">服务端编码</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> StopServer(string serverCode)
        {
            await _server.StopAsync(serverCode);
            return ApiReturnHelper.Success(null, $"服务端 {serverCode} 已停止");
        }

        /// <summary>
        /// 向指定客户端发送字节。
        /// </summary>
        /// <param name="input">发送入参</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> SendToClient([FromBody] SocketServerSendInput input)
        {
            await _server.SendToClientAsync(input.ServerCode, input.ClientId, input.Data ?? Array.Empty<byte>());
            return ApiReturnHelper.Success(null, "已发送");
        }

        /// <summary>
        /// 向指定客户端发送字符串（按服务端配置编码）。
        /// </summary>
        /// <param name="input">字符串发送入参</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> SendStringToClient([FromBody] SocketServerSendStringInput input)
        {
            await _server.SendToClientStringAsync(input.ServerCode, input.ClientId, input.Message ?? string.Empty);
            return ApiReturnHelper.Success(null, "已发送");
        }

        /// <summary>
        /// 向当前所有已连接客户端广播字节。
        /// </summary>
        /// <param name="input">广播入参</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> Broadcast([FromBody] SocketServerBroadcastInput input)
        {
            await _server.BroadcastAsync(input.ServerCode, input.Data ?? Array.Empty<byte>());
            return ApiReturnHelper.Success(null, "已广播");
        }

        /// <summary>
        /// 向当前所有已连接客户端广播字符串。
        /// </summary>
        /// <param name="input">字符串广播入参</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> BroadcastString([FromBody] SocketServerBroadcastStringInput input)
        {
            await _server.BroadcastStringAsync(input.ServerCode, input.Message ?? string.Empty);
            return ApiReturnHelper.Success(null, "已广播");
        }

        /// <summary>
        /// 获取指定服务端当前在线的客户端列表。
        /// </summary>
        /// <param name="serverCode">服务端编码</param>
        [HttpGet]
        public ApiUnifiedReturnStructure<List<string>> GetConnectedClients(string serverCode)
        {
            return ApiReturnHelper.Success(_server.GetConnectedClients(serverCode));
        }

        /// <summary>
        /// 获取指定服务端最近收到的消息（用于验证客户端上报的数据）。
        /// </summary>
        /// <param name="serverCode">服务端编码</param>
        /// <param name="take">返回最近条数，默认 20</param>
        [HttpGet]
        public ApiUnifiedReturnStructure<List<ReceivedSocketMessage>> GetRecentMessages(string serverCode, int take = 20)
        {
            return ApiReturnHelper.Success(_server.GetRecentMessages(serverCode, take));
        }

        /// <summary>
        /// 获取全部已加载的服务端编码列表。
        /// </summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<List<string>> GetAllServerCode()
        {
            return ApiReturnHelper.Success(_server.GetAllServerCodes());
        }
    }

    #region DTO

    /// <summary>向指定客户端发送字节入参</summary>
    public class SocketServerSendInput
    {
        /// <summary>服务端编码</summary>
        public string ServerCode { get; set; } = string.Empty;

        /// <summary>客户端标识（远端 IP:端口）</summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>待发送字节</summary>
        public byte[]? Data { get; set; }
    }

    /// <summary>向指定客户端发送字符串入参</summary>
    public class SocketServerSendStringInput
    {
        /// <summary>服务端编码</summary>
        public string ServerCode { get; set; } = string.Empty;

        /// <summary>客户端标识</summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>待发送字符串</summary>
        public string? Message { get; set; }
    }

    /// <summary>广播字节入参</summary>
    public class SocketServerBroadcastInput
    {
        /// <summary>服务端编码</summary>
        public string ServerCode { get; set; } = string.Empty;

        /// <summary>待广播字节</summary>
        public byte[]? Data { get; set; }
    }

    /// <summary>广播字符串入参</summary>
    public class SocketServerBroadcastStringInput
    {
        /// <summary>服务端编码</summary>
        public string ServerCode { get; set; } = string.Empty;

        /// <summary>待广播字符串</summary>
        public string? Message { get; set; }
    }

    #endregion
}
