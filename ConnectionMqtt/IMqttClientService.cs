using ConnectionMqtt.LocalEntity;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.LowLevelClient;

namespace ConnectionMqtt
{

    /// <summary>
    /// 参考文档：
    /// 1 多MQTT客户端封装+配置读取+DI注入完整实现（MQTTnet + ASP.NET Core）
    /// </summary>
    public interface IMqttClientService
    {
        /// <summary>
        /// 根据ClientId获取对应MQTT客户端
        /// </summary>
        IMqttClient GetClient(string clientId);

        /// <summary>
        /// 发布消息
        /// </summary>
        Task PublishAsync(string clientId, string topic, string payload);

        /// <summary>
        /// 订阅主题
        /// </summary>
        Task SubscribeAsync(string clientId, string topic, Func<MqttApplicationMessageReceivedEventArgs, Task> receiveHandler);

        /// <summary>
        /// 断开指定客户端
        /// </summary>
        Task DisconnectAsync(string clientId);

        /// <summary>
        /// 全部断开
        /// </summary>
        Task DisconnectAllAsync();

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        Task InitAllClientsAsync();

        /// <summary>
        /// 发送消息并等待指定主题响应（同步等待模式，带超时）
        /// </summary>
        /// <param name="clientId">mqtt客户端Id</param>
        /// <param name="topicSend">发送主题</param>
        /// <param name="sendPayload">发送内容</param>
        /// <param name="topicReply">等待响应的主题</param>
        /// <param name="timeoutMs">超时毫秒</param>
        /// <returns>(IsSuccess:是否收到响应, ResponsePayload:响应payload, IsTimeout:是否超时)</returns>
        Task<(bool IsSuccess, string? ResponsePayload, bool IsTimeout)> PublishAndWaitReplyAsync(
            string clientId,
            string topicSend,
            string sendPayload,
            string topicReply,
            int timeoutMs);

        /// <summary>
        /// 获取全部客户端内存状态（消除反射用）
        /// </summary>
        IEnumerable<(string ClientId, IMqttClient Client, MqttClientConfig Config)> GetAllMqttClients();

        /// <summary>
        /// 真实发送MQTT Ping，探测Broker是否可达，有网络IO
        /// </summary>
        Task<bool> PingMqttBrokerAsync(string clientId);

        Task UnSubscribeAsync(string clientId, string topic, Func<MqttApplicationMessageReceivedEventArgs, Task> receiveHandler);

        /// <summary>
        /// 获取全部已加载的 Socket 设备编码列表。
        /// </summary>
        List<string> GetAllDeviceCodes();

        /// <summary>
        /// 批量获取所有 MQTT 客户端与 Broker 的连接状态（Ping 探测）。
        /// </summary>
        Task<Dictionary<string, bool>> GetAllDeviceStatusAsync();


        /// <summary>
        /// Http接口托管式订阅，内部保存handler，避免匿名lambda无法解绑
        /// </summary>
        Task SubscribeManagedAsync(string clientId, string topic);

        /// <summary>
        /// Http接口托管式取消订阅，自动查找并移除内部保存的handler
        /// </summary>
        Task UnSubscribeManagedAsync(string clientId, string topic);

        /// <summary>
        /// 获取指定客户端 + 主题在托管订阅期间最近收到的消息列表（新的在前）。
        /// 需要先通过 SubscribeManagedAsync 订阅，消息由服务内部记录。
        /// </summary>
        /// <param name="clientId">MQTT客户端Id</param>
        /// <param name="topic">订阅主题</param>
        /// <returns>收到的消息列表（可能为空）</returns>
        Task<List<ReceivedMqttMessage>> GetReceivedMessagesAsync(string clientId, string topic);
    }
}