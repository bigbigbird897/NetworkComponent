using System;
using System.Collections.Generic;
using System.Text;

namespace ConnectionMqtt.LocalEntity
{
    /// <summary>
    /// 单个MQTT客户端配置
    /// </summary>
    public class MqttClientConfig
    {
        public string ClientId { get; set; } = string.Empty;
        public string ServerIp { get; set; } = string.Empty;
        public int Port { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool CleanSession { get; set; }
        public int KeepAliveSecond { get; set; }
    }

}
