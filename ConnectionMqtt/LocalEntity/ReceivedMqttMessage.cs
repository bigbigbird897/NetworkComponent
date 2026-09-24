namespace ConnectionMqtt.LocalEntity
{
    /// <summary>
    /// 托管订阅收到的 MQTT 消息记录（供 Web 控制台轮询展示）。
    /// </summary>
    public class ReceivedMqttMessage
    {
        /// <summary>消息主题</summary>
        public string Topic { get; set; } = string.Empty;

        /// <summary>消息内容（按 UTF-8 解码后的文本）</summary>
        public string Payload { get; set; } = string.Empty;

        /// <summary>接收时间（yyyy-MM-dd HH:mm:ss.fff）</summary>
        public string ReceiveTime { get; set; } = string.Empty;
    }
}
