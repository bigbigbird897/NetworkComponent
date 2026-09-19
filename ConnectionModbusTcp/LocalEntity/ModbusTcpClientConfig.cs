using System;
using System.Collections.Generic;
using System.Text;

namespace ConnectionModbusTcp.LocalEntity
{
    /// <summary>
    /// ModbusTCP设备配置（appsettings.json绑定）
    /// </summary>
    public class ModbusTcpClientConfig
    {
        /// <summary>
        /// 设备唯一编码
        /// </summary>
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary>
        /// IP地址
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// 端口
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 从站单元ID(Modbus‑TCP单元标识符)
        /// </summary>
        public byte SlaveId { get; set; }

        /// <summary>
        /// 超时毫秒
        /// </summary>
        public int TimeoutMs { get; set; }

        /// <summary>
        /// 是否等待应答
        /// </summary>
        public bool WaitResponse { get; set; }
    }
}
