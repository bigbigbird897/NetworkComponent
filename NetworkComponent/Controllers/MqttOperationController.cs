using Common.GlobalHelper;
using Common.LocalEntity;
using ConnectionMqtt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MQTTnet.Client;
using NetworkComponent.LocalEntity;
using Newtonsoft.Json.Linq;

namespace NetworkComponent.Controllers
{
    /// <summary>
    /// MQTT消息操作控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    //[ApiExplorerSettings(GroupName = "后台功能")]
    //[Authorize] // 需要JWT登录访问时打开
    public class MqttOperationController : ControllerBase
    {
        private readonly IMqttClientService _mqttService;
        private readonly ILogger? _logger;

        /// <summary>
        /// 构造函数，依赖注入MQTT服务与日志组件
        /// </summary>
        /// <param name="mqttService">MQTT客户端操作服务</param>
        /// <param name="logger">日志记录器</param>
        public MqttOperationController(IMqttClientService mqttService, ILogger<MqttOperationController> logger)
        {
            _logger = logger;
            _mqttService = mqttService;
        }

        /// <summary>
        /// 【发布MQTT消息】
        /// 请求方式：POST
        /// </summary>
        /// <param name="clientId">MQTT客户端标识ID</param>
        /// <param name="topic">要发布的主题</param>
        /// <param name="msg">消息内容</param>
        /// <returns>统一返回对象，返回发布成功提示</returns>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> PublishMsg(string clientId, string topic, string msg)
        {
            var message = HitbotMessage.CreateRequest<ParaMqttAxis>(
                "离心区z轴",
                new ParaMqttAxis { Position = 3.1415926 }
            );
            // 紧凑JSON（MQTT发送推荐）
            string jsonCompact = message.ToJsonString(true);
            await _mqttService.PublishAsync(clientId, topic, msg);
            return ApiReturnHelper.Success(null, "发布成功");
        }

        /// <summary>
        /// 【订阅MQTT主题】（托管模式，服务内部保存回调引用，支持正常取消订阅）
        /// 请求方式：POST
        /// </summary>
        /// <param name="clientId">MQTT客户端标识ID</param>
        /// <param name="topic">需要订阅的主题</param>
        /// <returns>统一返回对象，返回订阅成功提示；收到消息会在服务端日志输出</returns>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> SubTopic(string clientId, string topic)
        {
            await _mqttService.SubscribeManagedAsync(clientId, topic);
            return ApiReturnHelper.Success(null, "托管订阅成功");
        }

        /// <summary>
        /// 【取消订阅主题】（托管模式，自动移除本地回调 + Broker退订）
        /// </summary>
        /// <param name="clientId">MQTT客户端标识ID</param>
        /// <param name="topic">要取消订阅的主题</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> UnSubTopic(string clientId, string topic)
        {
            await _mqttService.UnSubscribeManagedAsync(clientId, topic);
            return ApiReturnHelper.Success(null, "托管取消订阅成功");
        }



        /// <summary>
        /// 【发送MQTT消息并等待指定主题响应（带超时控制）】
        /// 请求方式：POST
        /// </summary>
        /// <param name="input">发送等待应答请求入参实体</param>
        /// <returns>统一返回对象，包含是否成功、是否超时、应答JSON报文</returns>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<MqttWaitReplyResult>> PublishAndWaitReply([FromBody] MqttWaitReplyInput input)
        {
            if (input.TimeoutMs <= 0)
            {
                return ApiReturnHelper.ServerError(new MqttWaitReplyResult(), "超时时间必须大于0");
            }

            var result = await _mqttService.PublishAndWaitReplyAsync(
                clientId: input.ClientId,
                topicSend: input.TopicSend,
                sendPayload: input.SendPayload,
                topicReply: input.TopicReply,
                timeoutMs: input.TimeoutMs);

            var output = new MqttWaitReplyResult
            {
                IsSuccess = result.IsSuccess,
                IsTimeout = result.IsTimeout,
                // 应答可能为 null（超时场景），做容错解析
                ResponsePayload = string.IsNullOrEmpty(result.ResponsePayload)
                    ? null
                    : JObject.Parse(result.ResponsePayload)
            };

            return ApiReturnHelper.Success(output);
        }


        /// <summary>
        /// 获取全部已加载的  设备编码列表。
        /// </summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<List<string>> GetAllDeviceCode()
        {
            return ApiReturnHelper.Success(_mqttService.GetAllDeviceCodes());
        }
    }

    #region DTO

    /// <summary>
    /// MQTT发送并等待应答 请求入参
    /// </summary>
    public class MqttWaitReplyInput
    {
        /// <summary>MQTT客户端Id</summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>发送消息的主题</summary>
        public string TopicSend { get; set; } = string.Empty;

        /// <summary>发送报文内容</summary>
        public string SendPayload { get; set; } = string.Empty;

        /// <summary>等待接收应答的主题</summary>
        public string TopicReply { get; set; } = string.Empty;

        /// <summary>超时毫秒，例3000=3秒，必须大于0</summary>
        public int TimeoutMs { get; set; }
    }

    /// <summary>
    /// MQTT发送等待应答 返回结果实体
    /// </summary>
    public class MqttWaitReplyResult
    {
        /// <summary>是否成功收到应答报文</summary>
        public bool IsSuccess { get; set; }

        /// <summary>是否发生超时</summary>
        public bool IsTimeout { get; set; }

        /// <summary>应答报文JSON对象，超时场景下为null</summary>
        public JObject? ResponsePayload { get; set; }
    }

    #endregion
}