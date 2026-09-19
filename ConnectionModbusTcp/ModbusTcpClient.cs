using ConnectionModbusTcp.LocalEntity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ConnectionModbusTcp
{
    /// <summary>
    /// 标准 Modbus-TCP 客户端实现。
    /// 自 appsettings.json 的 "ModbusTcpConfigs" 节读取多设备配置，按 DeviceCode 缓存；
    /// 每次读写走短连接，按 MBAP+PDU 组包/解析。
    /// </summary>
    public class ModbusTcpClient : IModbusTcpClient
    {
        private readonly ILogger<ModbusTcpClient> _logger;
        private readonly ConcurrentDictionary<string, ModbusTcpClientConfig> _deviceDict = new();

        /// <summary>
        /// 构造函数：由 Autofac 注入配置与日志，启动时加载全部 ModbusTcp 设备配置。
        /// </summary>
        public ModbusTcpClient(ILogger<ModbusTcpClient> logger, IConfiguration config)
        {
            _logger = logger;
            var deviceConfigs = config.GetSection("ModbusTcpConfigs").Get<List<ModbusTcpClientConfig>>() ?? new();
            foreach (var dev in deviceConfigs)
            {
                if (!string.IsNullOrWhiteSpace(dev.DeviceCode))
                {
                    _deviceDict.TryAdd(dev.DeviceCode, dev);
                }
            }
            _logger.LogInformation("ModbusTcp 已加载设备数：{Count}", _deviceDict.Count);
        }

        #region IModbusTcpClient 接口实现
        public ModbusTcpClientConfig GetDeviceConfig(string deviceCode)
        {
            if (_deviceDict.TryGetValue(deviceCode, out var cfg))
                return cfg;
            throw new KeyNotFoundException($"未找到DeviceCode={deviceCode}的ModbusTcp设备配置");
        }

        public List<string> GetAllDeviceCodes()
        {
            return _deviceDict.Keys.ToList();
        }

        //public async Task<ushort[]> ReadHoldRegistersAsync(string deviceCode, ushort startAddr, ushort count)
        //{
        //    /*
        //     * [2026-08-23 15:14:10.873]# SEND HEX>
        //        00 01 00 00 00 06 01 03 00 00 00 02 

        //        [2026-08-23 15:14:10.888]# RECV HEX>
        //        00 01 00 00 00 07 01 03 04 00 10 00 00 
        //     */
        //    var cfg = GetDeviceConfig(deviceCode);

        //    // PDU: SlaveId(单元ID) + Func03 + StartAddr(2) + Count(2)
        //    /*
        //     * `>> 8`：**右移运算符**，把 16 位数字向右移动 8 个二进制位，把**高 8 位挪到低 8 位的位置**。
        //     * (byte)：强制转为byte（只保留低 8 位，取移动之后的值）
        //     * Modbus RTU / Modbus‑TCP PDU 地址：**大端（Big‑Endian），高字节先发，低字节后发**
        //     */
        //    byte[] pdu =
        //    {
        //        cfg.SlaveId,
        //        0x03,
        //        (byte)(startAddr >> 8),
        //        (byte)(startAddr & 0xFF),
        //        (byte)(count >> 8),
        //        (byte)(count & 0xFF)
        //    };
        //    // 组装MBAP头 + PDU
        //    ushort transactionId = (ushort)Random.Shared.Next(ushort.MaxValue);
        //    byte[] requestPacket = BuildModbusTcpPacket(transactionId, pdu);

        //    var respBytes = await SendRawTcpPacketAsync(deviceCode, requestPacket);

        //    // 解析应答：MBAP头7字节，后面PDU
        //    // MBAP:0‑6；PDU从索引7开始
        //    int mbapLen = 7;
        //    if (respBytes.Length < mbapLen + 2)
        //        throw new Exception($"设备{deviceCode}ModbusTcp应答报文长度不足");

        //    byte respFuncCode = respBytes[mbapLen];
        //    if ((respFuncCode & 0x80) != 0)
        //    {
        //        /*
        //         * 异常码	含义
        //            1	非法功能码：设备不支持该功能码
        //            2	非法数据地址：地址超出设备寄存器范围
        //            3	非法数据值：请求参数数值非法
        //         */
        //        byte errCode = respBytes[mbapLen + 1];
        //        throw new Exception($"ModbusTcp设备{deviceCode}异常，异常码:{errCode}");
        //    }

        //    int dataByteLen = respBytes[mbapLen + 1];
        //    ushort[] result = new ushort[dataByteLen / 2];
        //    for (int i = 0; i < result.Length; i++)
        //    {
        //        byte high = respBytes[mbapLen + 2 + i * 2];
        //        byte low = respBytes[mbapLen + 3 + i * 2];
        //        result[i] = (ushort)(high << 8 | low);
        //    }
        //    return result;
        //}
        public async Task<ushort[]> ReadHoldRegistersAsync(string deviceCode, ushort startAddr, ushort count)
        {
            var cfg = GetDeviceConfig(deviceCode);

            //        byte[] pdu =
            //        {
            //    cfg.SlaveId,
            //    0x03,
            //    (byte)(startAddr >> 8),
            //    (byte)(startAddr & 0xFF),
            //    (byte)(count >> 8),
            //    (byte)(count & 0xFF)
            //};
            //        ushort transactionId = (ushort)Random.Shared.Next(ushort.MaxValue);
            //        byte[] requestPacket = BuildModbusTcpPacket(transactionId, pdu);
            // ✅PDU只放功能码+参数，SlaveId(UnitId)传给BuildModbusTcpPacket第二个参数
            byte[] pdu =
            {
                0x03,
                (byte)(startAddr >> 8),
                (byte)(startAddr & 0xFF),
                (byte)(count >> 8),
                (byte)(count & 0xFF)
            };
            ushort transactionId = (ushort)Random.Shared.Next(ushort.MaxValue);
            byte[] requestPacket = BuildModbusTcpPacket(transactionId, cfg.SlaveId, pdu);


            var respBytes = await SendRawTcpPacketAsync(deviceCode, requestPacket);

            int mbapLen = 7;
            if (respBytes.Length < mbapLen + 2)
                throw new Exception($"设备{deviceCode}ModbusTcp应答报文长度不足");

            byte respFuncCode = respBytes[mbapLen];
            if ((respFuncCode & 0x80) != 0)
            {
                byte errCode = respBytes[mbapLen + 1];
                throw new Exception($"ModbusTcp设备{deviceCode}异常，异常码:{errCode}");
            }

            int dataByteLen = respBytes[mbapLen + 1];
            // 校验字节长度必须为偶数
            if (dataByteLen <= 0 || dataByteLen % 2 != 0)
            {
                throw new Exception($"设备{deviceCode}返回保持寄存器数据字节长度非法，dataByteLen={dataByteLen}");
            }
            // 校验缓冲区字节足够，防止报文截断越界
            int needTotalLen = mbapLen + 2 + dataByteLen;
            if (respBytes.Length < needTotalLen)
            {
                throw new Exception($"设备{deviceCode}应答报文截断，预期至少{needTotalLen}字节，实际收到{respBytes.Length}字节");
            }

            ushort[] result = new ushort[dataByteLen / 2];
            for (int i = 0; i < result.Length; i++)
            {
                byte high = respBytes[mbapLen + 2 + i * 2];
                byte low = respBytes[mbapLen + 3 + i * 2];
                result[i] = (ushort)(high << 8 | low);
            }
            return result;
        }


        public async Task WriteSingleRegisterAsync(string deviceCode, ushort addr, ushort value)
        {
            var cfg = GetDeviceConfig(deviceCode);
            //byte[] pdu =
            //{
            //    cfg.SlaveId,
            //    0x06,
            //    (byte)(addr >> 8),
            //    (byte)(addr & 0xFF),
            //    (byte)(value >> 8),
            //    (byte)(value & 0xFF)
            //};
            byte[] pdu =
            {
                0x06,
                (byte)(addr >> 8),
                (byte)(addr & 0xFF),
                (byte)(value >> 8),
                (byte)(value & 0xFF)
            };
            ushort transactionId = (ushort)Random.Shared.Next(ushort.MaxValue);
            byte[] reqPacket = BuildModbusTcpPacket(transactionId, cfg.SlaveId,pdu);

            var resp = await SendRawTcpPacketAsync(deviceCode, reqPacket);
            int mbapLen = 7;
            // MBAP 头 7 字节(6-后续字节长度，7-从站号) + PDU 共 5 字节
            if (resp.Length < mbapLen + 5)
                throw new Exception($"设备{deviceCode}单寄存器写入返回报文长度异常");

            // PDU应答应该和请求PDU一致
            byte[] respPdu = resp.Skip(mbapLen).Take(6).ToArray();
            byte[] reqPdu = pdu;
            if (!respPdu.SequenceEqual(reqPdu))
            {
                throw new Exception($"设备{deviceCode}单寄存器写入返回报文PDU不匹配");
            }
        }

        public async Task WriteMultiRegistersAsync(string deviceCode, ushort startAddr, List<ushort> values)
        {
            var cfg = GetDeviceConfig(deviceCode);
            List<byte> pduBuilder = new List<byte>
            {
                0x10,
                (byte)(startAddr >> 8),
                (byte)(startAddr & 0xFF),
                (byte)(values.Count >> 8),
                (byte)(values.Count & 0xFF),
                (byte)(values.Count * 2)
            };
            foreach (var val in values)
            {
                pduBuilder.Add((byte)(val >> 8));
                pduBuilder.Add((byte)(val & 0xFF));
            }
            byte[] pdu = pduBuilder.ToArray();

            ushort transactionId = (ushort)Random.Shared.Next(ushort.MaxValue);
            byte[] reqPacket = BuildModbusTcpPacket(transactionId, cfg.SlaveId, pdu);
            await SendRawTcpPacketAsync(deviceCode, reqPacket);
        }

        /// <summary>
        /// 01功能码：读取线圈。
        /// 应答：MBAP(7)+功能码+字节数+位数据，将位数据按小端位序解包为 bool[]。
        /// </summary>
        public async Task<bool[]> ReadCoilsAsync(string deviceCode, ushort startAddr, ushort count)
        {
            return await ReadBitStatusAsync(deviceCode, 0x01, startAddr, count);
        }

        /// <summary>
        /// 02功能码：读取离散输入，报文格式与01一致。
        /// </summary>
        public async Task<bool[]> ReadDiscreteInputsAsync(string deviceCode, ushort startAddr, ushort count)
        {
            return await ReadBitStatusAsync(deviceCode, 0x02, startAddr, count);
        }

        /// <summary>
        /// 04功能码：读取输入寄存器，报文格式与03一致。
        /// </summary>
        public async Task<ushort[]> ReadInputRegistersAsync(string deviceCode, ushort startAddr, ushort count)
        {
            return await ReadRegistersByFuncAsync(deviceCode, 0x04, startAddr, count);
        }

        /// <summary>
        /// 05功能码：写入单个线圈（ON=0xFF00，OFF=0x0000），设备应答应与请求PDU一致。
        /// </summary>
        public async Task WriteSingleCoilAsync(string deviceCode, ushort addr, bool value)
        {
            var cfg = GetDeviceConfig(deviceCode);
            ushort coilValue = value ? (ushort)0xFF00 : (ushort)0x0000;
            byte[] pdu =
            {
                0x05,
                (byte)(addr >> 8),
                (byte)(addr & 0xFF),
                (byte)(coilValue >> 8),
                (byte)(coilValue & 0xFF)
            };
            ushort transactionId = (ushort)Random.Shared.Next(ushort.MaxValue);
            byte[] reqPacket = BuildModbusTcpPacket(transactionId, cfg.SlaveId, pdu);

            var resp = await SendRawTcpPacketAsync(deviceCode, reqPacket);
            int mbapLen = 7;
            if (resp.Length < mbapLen + 5)
                throw new Exception($"设备{deviceCode}写单线圈返回报文长度异常");

            byte[] respPdu = resp.Skip(mbapLen).Take(5).ToArray();
            if (!respPdu.SequenceEqual(pdu))
                throw new Exception($"设备{deviceCode}写单线圈返回报文PDU不匹配");
        }

        /// <summary>
        /// 0F功能码：批量写入多个线圈。bool[] 按位打包成字节（低位在前）。
        /// </summary>
        public async Task WriteMultiCoilsAsync(string deviceCode, ushort startAddr, bool[] values)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("写入线圈列表不能为空", nameof(values));

            var cfg = GetDeviceConfig(deviceCode);
            int quantity = values.Length;
            int byteCount = (quantity + 7) / 8;
            byte[] packed = new byte[byteCount];
            for (int i = 0; i < quantity; i++)
            {
                if (values[i])
                    packed[i / 8] |= (byte)(1 << (i % 8));
            }

            List<byte> pduBuilder = new List<byte>
            {
                0x0F,
                (byte)(startAddr >> 8),
                (byte)(startAddr & 0xFF),
                (byte)(quantity >> 8),
                (byte)(quantity & 0xFF),
                (byte)byteCount
            };
            pduBuilder.AddRange(packed);
            byte[] pdu = pduBuilder.ToArray();

            ushort transactionId = (ushort)Random.Shared.Next(ushort.MaxValue);
            byte[] reqPacket = BuildModbusTcpPacket(transactionId, cfg.SlaveId, pdu);
            await SendRawTcpPacketAsync(deviceCode, reqPacket);
        }

        /// <summary>
        /// 位状态读取公共实现（01读线圈 / 02读离散输入）。
        /// </summary>
        private async Task<bool[]> ReadBitStatusAsync(string deviceCode, byte funcCode, ushort startAddr, ushort count)
        {
            var cfg = GetDeviceConfig(deviceCode);
            byte[] pdu =
            {
                funcCode,
                (byte)(startAddr >> 8),
                (byte)(startAddr & 0xFF),
                (byte)(count >> 8),
                (byte)(count & 0xFF)
            };
            ushort transactionId = (ushort)Random.Shared.Next(ushort.MaxValue);
            byte[] requestPacket = BuildModbusTcpPacket(transactionId, cfg.SlaveId, pdu);

            var respBytes = await SendRawTcpPacketAsync(deviceCode, requestPacket);

            int mbapLen = 7;
            if (respBytes.Length < mbapLen + 2)
                throw new Exception($"设备{deviceCode}ModbusTcp应答报文长度不足");

            byte respFuncCode = respBytes[mbapLen];
            if ((respFuncCode & 0x80) != 0)
                throw new Exception($"ModbusTcp设备{deviceCode}异常，异常码:{respBytes[mbapLen + 1]}");

            int dataByteLen = respBytes[mbapLen + 1];
            int needTotalLen = mbapLen + 2 + dataByteLen;
            if (respBytes.Length < needTotalLen)
                throw new Exception($"设备{deviceCode}应答报文截断，预期至少{needTotalLen}字节，实际收到{respBytes.Length}字节");

            // 将返回的位字节流按“每字节低位在前”解包为 bool[count]
            bool[] result = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int byteIdx = i / 8;
                int bitIdx = i % 8;
                byte b = respBytes[mbapLen + 2 + byteIdx];
                result[i] = (b & (1 << bitIdx)) != 0;
            }
            return result;
        }

        /// <summary>
        /// 寄存器读取公共实现（03读保持寄存器 / 04读输入寄存器）。
        /// </summary>
        private async Task<ushort[]> ReadRegistersByFuncAsync(string deviceCode, byte funcCode, ushort startAddr, ushort count)
        {
            var cfg = GetDeviceConfig(deviceCode);
            byte[] pdu =
            {
                funcCode,
                (byte)(startAddr >> 8),
                (byte)(startAddr & 0xFF),
                (byte)(count >> 8),
                (byte)(count & 0xFF)
            };
            ushort transactionId = (ushort)Random.Shared.Next(ushort.MaxValue);
            byte[] requestPacket = BuildModbusTcpPacket(transactionId, cfg.SlaveId, pdu);

            var respBytes = await SendRawTcpPacketAsync(deviceCode, requestPacket);

            int mbapLen = 7;
            if (respBytes.Length < mbapLen + 2)
                throw new Exception($"设备{deviceCode}ModbusTcp应答报文长度不足");

            byte respFuncCode = respBytes[mbapLen];
            if ((respFuncCode & 0x80) != 0)
                throw new Exception($"ModbusTcp设备{deviceCode}异常，异常码:{respBytes[mbapLen + 1]}");

            int dataByteLen = respBytes[mbapLen + 1];
            if (dataByteLen <= 0 || dataByteLen % 2 != 0)
                throw new Exception($"设备{deviceCode}返回寄存器数据字节长度非法，dataByteLen={dataByteLen}");

            int needTotalLen = mbapLen + 2 + dataByteLen;
            if (respBytes.Length < needTotalLen)
                throw new Exception($"设备{deviceCode}应答报文截断，预期至少{needTotalLen}字节，实际收到{respBytes.Length}字节");

            ushort[] result = new ushort[dataByteLen / 2];
            for (int i = 0; i < result.Length; i++)
            {
                byte high = respBytes[mbapLen + 2 + i * 2];
                byte low = respBytes[mbapLen + 3 + i * 2];
                result[i] = (ushort)(high << 8 | low);
            }
            return result;
        }

        public async Task<byte[]> SendRawTcpPacketAsync(string deviceCode, byte[] tcpPacketBytes)
        {
            var cfg = GetDeviceConfig(deviceCode);
            try
            {
                _logger.LogInformation("ModbusTcp[{DeviceCode}] 发送报文: {Hex}",
                    deviceCode, BitConverter.ToString(tcpPacketBytes));

                var response = await InnerTcpSendAsync(
                    cfg.IpAddress, cfg.Port, tcpPacketBytes, cfg.WaitResponse, cfg.TimeoutMs);

                _logger.LogInformation("ModbusTcp[{DeviceCode}] 返回报文: {Hex}",
                    deviceCode, BitConverter.ToString(response));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ModbusTcp[{DeviceCode}]通信异常 Ip:{Ip}:{Port}",
                    deviceCode, cfg.IpAddress, cfg.Port);
                throw;
            }
        }
        #endregion

        #region 私有辅助
        /// <summary>
        /// 组装完整Modbus‑TCP报文 MBAP头 + PDU
        /// </summary>
        //private byte[] BuildModbusTcpPacket(ushort transactionId, byte[] pdu)
        //{
        //    // MBAP Header:
        //    // 0‑1 Transaction Identifier
        //    // 2‑3 Protocol Identifier (0)
        //    // 4‑5 Length:后续字节总长度 = UnitId + PDU
        //    // 6 Unit Identifier
        //    ushort length = (ushort)pdu.Length;
        //    byte[] mbap = new byte[7];
        //    mbap[0] = (byte)(transactionId >> 8);
        //    mbap[1] = (byte)(transactionId & 0xFF);
        //    mbap[2] = 0x00;
        //    mbap[3] = 0x00;
        //    mbap[4] = (byte)(length >> 8);
        //    mbap[5] = (byte)(length & 0xFF);
        //    return mbap.Concat(pdu).ToArray();
        //}

        /// <summary>
        /// 组装完整Modbus‑TCP报文 MBAP头 + PDU
        /// PDU传入：功能码+参数，**不包含UnitId**；UnitId放在MBAP[6]
        /// </summary>
        /// <param name="transactionId">事务ID</param>
        /// <param name="unitId">从站单元ID</param>
        /// <param name="pdu">PDU：功能码+参数，不带UnitId</param>
        /// <returns>完整MBAP+PDU报文</returns>
        private byte[] BuildModbusTcpPacket(ushort transactionId, byte unitId, byte[] pdu)
        {
            // Length = unitId(1) + pdu.Length
            ushort length = (ushort)(1 + pdu.Length);
            byte[] mbap = new byte[7];
            mbap[0] = (byte)(transactionId >> 8);
            mbap[1] = (byte)(transactionId & 0xFF);
            mbap[2] = 0x00;
            mbap[3] = 0x00;
            mbap[4] = (byte)(length >> 8);
            mbap[5] = (byte)(length & 0xFF);
            mbap[6] = unitId; // ✅ UnitId放到MBAP头第7字节,从站号
            return mbap.Concat(pdu).ToArray();
        }


        /// <summary>
        /// 底层TCP收发，和RTU‑over‑tcp保持一致socket逻辑
        /// </summary>
        private async Task<byte[]> InnerTcpSendAsync(string serverIp, int port, byte[] sendData, bool waitResponse = true, int timeoutMs = 1000)
        {
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            List<byte> recvDatas = new List<byte>();
            var pool = ArrayPool<byte>.Shared;
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                IPAddress ipAddress = IPAddress.Parse(serverIp);
                IPEndPoint serverEp = new IPEndPoint(ipAddress, port);
                await socket.ConnectAsync(serverEp).ConfigureAwait(false);
                await socket.SendAsync(sendData, SocketFlags.None, tokenSource.Token).ConfigureAwait(false);

                if (waitResponse)
                {
                    byte[] buffer = pool.Rent(1024);
                    Task delayTask = Task.Delay(timeoutMs, tokenSource.Token);
                    Task recvTask = Task.Run(async () =>
                    {
                        int readLen = await socket.ReceiveAsync(buffer, SocketFlags.None, tokenSource.Token).ConfigureAwait(false);
                        recvDatas.AddRange(buffer.Take(readLen));
                    }, tokenSource.Token);

                    await Task.WhenAny(delayTask, recvTask);
                    if (delayTask.IsCompleted)
                    {
                        tokenSource.Cancel();
                        _logger.LogWarning("ModbusTcp TCP通信超时 Ip:{Ip}:{Port},Timeout:{Timeout}ms", serverIp, port, timeoutMs);
                    }
                    pool.Return(buffer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InnerTcpSendAsync 异常 Ip:{Ip}:{Port}", serverIp, port);
            }
            finally
            {
                if (socket != null && socket.Connected)
                {
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                }
                socket?.Dispose();
            }
            return recvDatas.ToArray();
        }
        #endregion
    }
}
