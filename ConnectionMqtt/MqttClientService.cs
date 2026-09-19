using ConnectionMqtt.LocalEntity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using System.Collections.Concurrent;


namespace ConnectionMqtt
{
    public class MqttClientService : IMqttClientService
    {
        // 托管http接口的订阅关系 key=clientId||topic
        private readonly ConcurrentDictionary<string, (string ClientId, string Topic, Func<MqttApplicationMessageReceivedEventArgs, Task> Handler)> _managedSubscribeDict = new();

        private readonly IConfiguration _config;
        private readonly ILogger<MqttClientService> _logger;
        // 缓存所有MQTT客户端：ClientId -> IMqttClient
        private readonly ConcurrentDictionary<string, IMqttClient> _clientDict = new();
        private readonly List<MqttClientConfig> _mqttConfigs = new();

        public MqttClientService(IConfiguration config, ILogger<MqttClientService> logger)
        {
            _config = config;
            _logger = logger;
            // 读取多MQTT配置
            _mqttConfigs = _config.GetSection("MqttConfigs").Get<List<MqttClientConfig>>() ?? new();
        }

        /// <summary>
        /// 程序启动时自动创建并连接所有MQTT客户端
        /// </summary>
        public async Task InitAllClientsAsync()
        {
            foreach (var cfg in _mqttConfigs)
            {
                await CreateAndConnectClientAsync(cfg);
            }
        }

        public IMqttClient GetClient(string clientId)
        {
            if (_clientDict.TryGetValue(clientId, out var client))
                return client;
            throw new KeyNotFoundException($"不存在ClientId:{clientId}的MQTT客户端");
        }

        public async Task PublishAsync(string clientId, string topic, string payload)
        {
            var client = GetClient(clientId);
            if (!client.IsConnected)
            {
                _logger.LogWarning("MQTT客户端{ClientId}未连接，尝试重连", clientId);
                await ReconnectClientAsync(clientId);
            }

            var msg = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await client.PublishAsync(msg);
            _logger.LogDebug("MQTT[{ClientId}] 发布主题:{Topic},内容:{Payload}", clientId, topic, payload);
        }

        public async Task SubscribeAsync(string clientId, string topic, Func<MqttApplicationMessageReceivedEventArgs, Task> receiveHandler)
        {
            var client = GetClient(clientId);
            // 注册消息接收回调
            client.ApplicationMessageReceivedAsync += receiveHandler;
            // 订阅主题
            await client.SubscribeAsync(topic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            _logger.LogInformation("MQTT[{ClientId}] 订阅主题:{Topic}", clientId, topic);
        }

        public async Task DisconnectAsync(string clientId)
        {
            if (_clientDict.TryGetValue(clientId, out var client) && client.IsConnected)
            {
                await client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build());
                _logger.LogInformation("MQTT[{ClientId}] 已断开连接", clientId);
            }
        }

        public async Task DisconnectAllAsync()
        {
            foreach (var kv in _managedSubscribeDict)
            {
                var client = GetClient(kv.Value.ClientId);
                client.ApplicationMessageReceivedAsync -= kv.Value.Handler;
            }
            _managedSubscribeDict.Clear();

            foreach (var kv in _clientDict)
            {
                await DisconnectAsync(kv.Key);
            }
            _clientDict.Clear();
        }


        #region 内部创建/重连逻辑
        private async Task CreateAndConnectClientAsync(MqttClientConfig cfg)
        {
            var factory = new MqttFactory();
            var mqttClient = factory.CreateMqttClient();

            // 配置连接参数
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(cfg.ServerIp, cfg.Port)
                .WithClientId(cfg.ClientId)
                .WithCleanSession(cfg.CleanSession)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(cfg.KeepAliveSecond));

            if (!string.IsNullOrEmpty(cfg.UserName))
            {
                optionsBuilder.WithCredentials(cfg.UserName, cfg.Password);
            }

            var options = optionsBuilder.Build();

            // 断线自动重连事件
            mqttClient.DisconnectedAsync += async e =>
            {
                _logger.LogError("MQTT[{ClientId}] 连接断开，原因:{Reason}", cfg.ClientId, e.Reason);
                await Task.Delay(3000);
                await ReconnectClientAsync(cfg.ClientId);
            };

            // 建立连接
            await mqttClient.ConnectAsync(options);
            _logger.LogInformation("MQTT[{ClientId}] 连接服务器成功 {Ip}:{Port}", cfg.ClientId, cfg.ServerIp, cfg.Port);

            // 存入缓存
            _clientDict.TryAdd(cfg.ClientId, mqttClient);
        }

        private async Task ReconnectClientAsync(string clientId)
        {
            var cfg = _mqttConfigs.FirstOrDefault(x => x.ClientId == clientId);
            if (cfg == null) return;

            try
            {
                await CreateAndConnectClientAsync(cfg);
                _logger.LogInformation("MQTT[{ClientId}] 重连成功", clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT[{ClientId}] 重连失败", clientId);
            }
        }
        #endregion


        /// <summary>
        /// 发布消息，等待指定主题返回响应，带超时
        /// </summary>
        public async Task<(bool IsSuccess, string? ResponsePayload, bool IsTimeout)> PublishAndWaitReplyAsync(
            string clientId,
            string topicSend,
            string sendPayload,
            string topicReply,
            int timeoutMs)
        {
            var client = GetClient(clientId);
            if (!client.IsConnected)
            {
                _logger.LogWarning("MQTT[{ClientId}]未连接，PublishAndWaitReplyAsync尝试重连", clientId);
                await ReconnectClientAsync(clientId);
                if (!client.IsConnected)
                {
                    return (false, null, false);
                }
            }

            var tcs = new TaskCompletionSource<string?>();
            // cts用于超时取消
            using var cts = new CancellationTokenSource(timeoutMs);

            // 局部回调，收到匹配主题消息完成tcs
            Func<MqttApplicationMessageReceivedEventArgs, Task> tempReceiveHandler = args =>
            {
                if (args.ApplicationMessage.Topic == topicReply)
                {
                    //var payload = args.ApplicationMessage.PayloadSegment.ToString();
                    //tcs.TrySetResult(payload);
                    // 正确：从 PayloadSegment 获取字节，转UTF‑8字符串
                    var payloadBytes = args.ApplicationMessage.PayloadSegment;
                    var payload = System.Text.Encoding.UTF8.GetString(payloadBytes.AsSpan());
                    tcs.TrySetResult(payload);
                }
                return Task.CompletedTask;
            };

            try
            {
                //注册临时接收事件
                client.ApplicationMessageReceivedAsync += tempReceiveHandler;

                //发布发送消息
                await PublishAsync(clientId, topicSend, sendPayload);
                _logger.LogDebug("MQTT[{ClientId}] PublishAndWaitReplyAsync 已发送 {SendTopic},等待响应主题:{ReplyTopic},超时{Timeout}ms",
                    clientId, topicSend, topicReply, timeoutMs);

                //等待：要么收到消息完成tcs，要么超时
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs, cts.Token));

                if (completedTask == tcs.Task)
                {
                    //收到响应
                    var resp = await tcs.Task;
                    return (true, resp, false);
                }
                else
                {
                    //超时
                    _logger.LogWarning("MQTT[{ClientId}] PublishAndWaitReplyAsync 等待响应超时，发送主题:{SendTopic},响应主题:{ReplyTopic}",
                        clientId, topicSend, topicReply);
                    return (false, null, true);
                }
            }
            finally
            {
                //⚠️关键：一定要解绑事件，否则多次调用会不断叠加回调，内存泄漏
                client.ApplicationMessageReceivedAsync -= tempReceiveHandler;
                cts.Cancel();
            }
        }


        /// <summary>
        /// 获取全部客户端内存状态（消除反射用）
        /// </summary>
        public IEnumerable<(string ClientId, IMqttClient Client, MqttClientConfig Config)> GetAllMqttClients()
        {
            foreach (var kv in _clientDict)
            {
                var cfg = _mqttConfigs.FirstOrDefault(c => c.ClientId == kv.Key);
                if (cfg != null)
                {
                    yield return (kv.Key, kv.Value, cfg);
                }
            }
        }

        /// <summary>
        /// 真实发送MQTT Ping，探测Broker是否可达，有网络IO
        /// </summary>
        public async Task<bool> PingMqttBrokerAsync(string clientId)
        {
            try
            {
                var client = GetClient(clientId);

                // 内存标记未连接，先尝试重连
                if (!client.IsConnected)
                {
                    _logger.LogWarning("PingMqttBrokerAsync: MQTT[{ClientId}]内存标记未连接，执行重连", clientId);
                    await ReconnectClientAsync(clientId);
                    // 重连之后再次判断
                    if (!client.IsConnected)
                    {
                        _logger.LogWarning("PingMqttBrokerAsync: MQTT[{ClientId}]重连失败，Ping返回false", clientId);
                        return false;
                    }
                }

                // ✅发送 MQTT PINGREQ，等待 PINGRESP
                // MQTTnet内部自带超时，受KeepAlivePeriod约束
                await client.PingAsync();
                _logger.LogDebug("PingMqttBrokerAsync: MQTT[{ClientId}] Ping成功，Broker可达", clientId);
                return true;
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("PingMqttBrokerAsync: MQTT[{ClientId}] 不存在该客户端", clientId);
                return false;
            }
            catch (Exception ex)
            {
                // 僵死连接场景：IsConnected=true，但实际TCP已经断开，PingAsync抛异常
                // MQTTnet内部会触发DisconnectedAsync事件，自动更新IsConnected为false
                _logger.LogError(ex, "PingMqttBrokerAsync: MQTT[{ClientId}] Ping探测失败，网络异常", clientId);
                return false;
            }
        }


        /// <summary>
        /// 取消主题订阅；取消Broker侧topic订阅，并且移除本地注册的回调处理器
        /// </summary>
        /// <param name="clientId">mqtt客户端Id</param>
        /// <param name="topic">待取消订阅主题</param>
        /// <param name="receiveHandler">需要移除的回调委托，必须和Subscribe传入同一个委托实例</param>
        public async Task UnSubscribeAsync(string clientId, string topic, Func<MqttApplicationMessageReceivedEventArgs, Task> receiveHandler)
        {
            var client = GetClient(clientId);

            // ① 从Mqtt客户端事件中移除回调处理器，防止内存泄漏
            client.ApplicationMessageReceivedAsync -= receiveHandler;

            // ② 向Broker发送取消订阅报文
            await client.UnsubscribeAsync(topic);

            _logger.LogInformation("MQTT[{ClientId}] 已取消订阅主题:{Topic}", clientId, topic);
        }

        /// <summary>
        /// Http接口托管式订阅，消息仅打印日志
        /// </summary>
        public async Task SubscribeManagedAsync(string clientId, string topic)
        {
            var client = GetClient(clientId);
            string dictKey = $"{clientId}||{topic}";
            // 防止重复订阅同一个client+topic
            if (_managedSubscribeDict.ContainsKey(dictKey))
            {
                _logger.LogWarning("MQTT[{ClientId}] 主题 {Topic} 已经是托管订阅状态，跳过", clientId, topic);
                return;
            }

            // 定义handler
            Func<MqttApplicationMessageReceivedEventArgs, Task> handler = async args =>
            {
                var payloadBytes = args.ApplicationMessage.PayloadSegment;
                string payload = System.Text.Encoding.UTF8.GetString(payloadBytes.AsSpan());
                string t = args.ApplicationMessage.Topic;
                _logger.LogInformation($"[托管订阅]收到消息 主题:{t} 内容:{payload}");
                await Task.CompletedTask;
            };

            // 注册事件 + 订阅broker
            client.ApplicationMessageReceivedAsync += handler;
            await client.SubscribeAsync(topic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);

            //存入托管字典
            _managedSubscribeDict.TryAdd(dictKey, (clientId, topic, handler));
            _logger.LogInformation("MQTT[{ClientId}] 托管订阅主题:{Topic}", clientId, topic);
        }

        /// <summary>
        /// Http接口托管式取消订阅：解绑本地事件 + broker退订
        /// </summary>
        public async Task UnSubscribeManagedAsync(string clientId, string topic)
        {
            var client = GetClient(clientId);
            string dictKey = $"{clientId}||{topic}";
            if (!_managedSubscribeDict.TryRemove(dictKey, out var item))
            {
                _logger.LogWarning("MQTT[{ClientId}] 找不到托管订阅记录，topic={Topic}", clientId, topic);
                // 兜底：依然执行broker退订
                await client.UnsubscribeAsync(topic);
                return;
            }

            // 移除事件委托（关键，防止内存泄漏）
            client.ApplicationMessageReceivedAsync -= item.Handler;
            // broker取消订阅
            await client.UnsubscribeAsync(topic);
            _logger.LogInformation("MQTT[{ClientId}] 托管取消订阅主题:{Topic}", clientId, topic);
        }

        public List<string> GetAllDeviceCodes()=> _clientDict.Keys.ToList();
    }

}
