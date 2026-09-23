using System;

namespace ConnectionSocket.LocalEntity
{
    /// <summary>
    /// 长连接实时状态快照，供接口/界面查询某一设备当前的长连接情况。
    /// </summary>
    public class LongConnectionStatus
    {
        /// <summary>设备编码</summary>
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary>长连接是否已建立并处于打开状态</summary>
        public bool IsOpen { get; set; }

        /// <summary>远端 IP</summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>远端端口</summary>
        public int Port { get; set; }

        /// <summary>本次连接建立时间（未连接时为空）</summary>
        public string? ConnectedSince { get; set; }

        /// <summary>本次连接累计发送字节数</summary>
        public long BytesSent { get; set; }

        /// <summary>本次连接累计接收字节数</summary>
        public long BytesReceived { get; set; }
    }
}
