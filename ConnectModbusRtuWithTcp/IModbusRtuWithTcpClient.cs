using ConnectionModbusRtuWithTcp.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConnectionModbusRtuWithTcp
{
    /// <summary>
    /// Modbus RTU over TCP 客户端业务接口
    /// 封装RTU报文组装、CRC校验、寄存器读写，底层复用TCP通用通信库
    /// </summary>
    public interface IModbusRtuWithTcpClient
    {
        /// <summary>
        /// 根据设备编码获取对应设备配置
        /// </summary>
        /// <param name="deviceCode">设备唯一编码</param>
        ModbusRtuWithTcpClientConfig GetDeviceConfig(string deviceCode);

        /// <summary>
        /// 03功能码：读取保持寄存器
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="startAddr">起始寄存器地址</param>
        /// <param name="count">读取寄存器数量</param>
        /// <returns>寄存器原始ushort数组</returns>
        Task<ushort[]> ReadHoldRegistersAsync(string deviceCode, ushort startAddr, ushort count);

        /// <summary>
        /// 06功能码：写入单个保持寄存器
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="addr">寄存器地址</param>
        /// <param name="value">写入值</param>
        Task WriteSingleRegisterAsync(string deviceCode, ushort addr, ushort value);

        /// <summary>
        /// 10功能码：批量写入多个保持寄存器
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="startAddr">起始地址</param>
        /// <param name="values">待写入寄存器数组</param>
        Task WriteMultiRegistersAsync(string deviceCode, ushort startAddr, ushort[] values);

        /// <summary>
        /// 原始发送RTU报文（自定义功能码场景使用）
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="rtuBytes">完整RTU报文（含从站、功能码、CRC16）</param>
        /// <returns>设备返回原始字节</returns>
        Task<byte[]> SendRawRtuPacketAsync(string deviceCode, byte[] rtuBytes);


        List<string> GetAllDeviceCodes();
    }
}
