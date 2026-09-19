using System;
using System.Collections.Generic;
using System.Text;

namespace ConnectionModbusRtuWithTcp.LocalEntity
{
    /// <summary>
    /// Modbus RTU over TCP 客户端连接配置
    /// 参考文档：
    /// 1 Modbus从站地址完整详解
    /// 2 实现ModbusRtuWithTcpClient
    /// </summary>
    public class ModbusRtuWithTcpClientConfig
    {
        /// <summary>
        /// 设备唯一标识（用于DI内部分组区分多设备）
        /// </summary>
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary>
        /// TCP远端设备IP
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// TCP端口
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Modbus从站地址 1~247
        /// </summary>
        public byte SlaveId { get; set; }

        /// <summary>
        /// 通信超时(毫秒)
        /// </summary>
        public int TimeoutMs { get; set; } = 1000;

        /// <summary>
        /// 发送指令后是否等待设备返回报文
        /// </summary>
        public bool WaitResponse { get; set; } = true;
    }

}
