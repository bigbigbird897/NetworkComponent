using ConnectionModbusRtuWithTcp.LocalHelper;
using ConnectionModbusRtuWithTcp.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace ConnectionModbusRtuWithTcp
{
    /// <summary>
    /// Modbus RTU over TCP 客户端实现。
    /// 自 appsettings.json 的 "ModbusRtuWithTcpConfigs" 节读取多设备配置；
    /// 在 TCP 链路上收发标准 RTU 报文（含 CRC16 校验）。
    /// </summary>
    public class ModbusRtuWithTcpClient : IModbusRtuWithTcpClient
    {
        private readonly ILogger<ModbusRtuWithTcpClient> _logger;
        private readonly ConcurrentDictionary<string, ModbusRtuWithTcpClientConfig> _deviceDict = new();

        /// <summary>
        /// 构造函数：由 Autofac 注入配置与日志，启动时加载全部 Modbus RTU 设备配置。
        /// </summary>
        public ModbusRtuWithTcpClient(ILogger<ModbusRtuWithTcpClient> logger, IConfiguration config)
        {
            _logger = logger;
            var deviceConfigs = config.GetSection("ModbusRtuWithTcpConfigs").Get<List<ModbusRtuWithTcpClientConfig>>() ?? new();
            foreach (var dev in deviceConfigs)
            {
                if (!string.IsNullOrWhiteSpace(dev.DeviceCode))
                {
                    _deviceDict.TryAdd(dev.DeviceCode, dev);
                }
            }
            _logger.LogInformation("ModbusRtuWithTcp 已加载设备数：{Count}", _deviceDict.Count);
        }

        #region 对外业务接口
        public ModbusRtuWithTcpClientConfig GetDeviceConfig(string deviceCode)
        {
            if (_deviceDict.TryGetValue(deviceCode, out var cfg))
                return cfg;
            throw new KeyNotFoundException($"未找到DeviceCode={deviceCode}的Modbus设备配置");
        }

        public List<string> GetAllDeviceCodes()
        {
            return _deviceDict.Keys.ToList();
        }

        public async Task<ushort[]> ReadHoldRegistersAsync(string deviceCode, ushort startAddr, ushort count)
        {
            var cfg = GetDeviceConfig(deviceCode);
            List<byte> reqBody = new List<byte>
            {
                cfg.SlaveId,
                0x03,
                (byte)(startAddr >> 8),
                (byte)(startAddr & 0xFF),
                (byte)(count >> 8),
                (byte)(count & 0xFF)
            };
            var crc = ModbusCrcHelper.CalcCrc(reqBody.ToArray());
            reqBody.AddRange(crc);

            var respBytes = await SendRawRtuPacketAsync(deviceCode, reqBody.ToArray());

            if (!ModbusCrcHelper.CheckCrc(respBytes, respBytes.Length))
                throw new Exception($"设备{deviceCode}返回报文CRC校验失败");

            if ((respBytes[1] & 0x80) != 0)
            {
                byte errCode = respBytes[2];
                throw new Exception($"Modbus设备{deviceCode}异常，异常码:{errCode}");
            }

            int dataLen = respBytes[2];
            ushort[] result = new ushort[dataLen / 2];
            for (int i = 0; i < result.Length; i++)
            {
                byte high = respBytes[3 + i * 2];
                byte low = respBytes[4 + i * 2];
                result[i] = (ushort)(high << 8 | low);
            }
            return result;
        }

        public async Task WriteSingleRegisterAsync(string deviceCode, ushort addr, ushort value)
        {
            var cfg = GetDeviceConfig(deviceCode);
            List<byte> reqBody = new List<byte>
            {
                cfg.SlaveId,
                0x06,
                (byte)(addr >> 8),
                (byte)(addr & 0xFF),
                (byte)(value >> 8),
                (byte)(value & 0xFF)
            };
            var crc = ModbusCrcHelper.CalcCrc(reqBody.ToArray());
            reqBody.AddRange(crc);

            var resp = await SendRawRtuPacketAsync(deviceCode, reqBody.ToArray());
            if (!resp.Take(6).SequenceEqual(reqBody.Take(6)))
                throw new Exception($"设备{deviceCode}单寄存器写入返回报文不匹配");
        }

        public async Task WriteMultiRegistersAsync(string deviceCode, ushort startAddr, ushort[] values)
        {
            var cfg = GetDeviceConfig(deviceCode);
            List<byte> reqBody = new List<byte>
            {
                cfg.SlaveId,
                0x10,
                (byte)(startAddr >> 8),
                (byte)(startAddr & 0xFF),
                (byte)(values.Length >> 8),
                (byte)(values.Length & 0xFF),
                (byte)(values.Length * 2)
            };
            foreach (var val in values)
            {
                reqBody.Add((byte)(val >> 8));
                reqBody.Add((byte)(val & 0xFF));
            }
            var crc = ModbusCrcHelper.CalcCrc(reqBody.ToArray());
            reqBody.AddRange(crc);

            await SendRawRtuPacketAsync(deviceCode, reqBody.ToArray());
        }

        /// <summary>
        /// 01功能码：读取线圈。RTU应答：从站(1)+功能码(1)+字节数(1)+位数据(...)。
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
        /// 05功能码：写入单个线圈（ON=0xFF00，OFF=0x0000），设备应答应与请求一致。
        /// </summary>
        public async Task WriteSingleCoilAsync(string deviceCode, ushort addr, bool value)
        {
            var cfg = GetDeviceConfig(deviceCode);
            ushort coilValue = value ? (ushort)0xFF00 : (ushort)0x0000;
            List<byte> reqBody = new List<byte>
            {
                cfg.SlaveId,
                0x05,
                (byte)(addr >> 8),
                (byte)(addr & 0xFF),
                (byte)(coilValue >> 8),
                (byte)(coilValue & 0xFF)
            };
            var crc = ModbusCrcHelper.CalcCrc(reqBody.ToArray());
            reqBody.AddRange(crc);

            var resp = await SendRawRtuPacketAsync(deviceCode, reqBody.ToArray());
            if (!resp.Take(8).SequenceEqual(reqBody.Take(8)))
                throw new Exception($"设备{deviceCode}写单线圈返回报文不匹配");
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

            List<byte> reqBody = new List<byte>
            {
                cfg.SlaveId,
                0x0F,
                (byte)(startAddr >> 8),
                (byte)(startAddr & 0xFF),
                (byte)(quantity >> 8),
                (byte)(quantity & 0xFF),
                (byte)byteCount
            };
            reqBody.AddRange(packed);
            var crc = ModbusCrcHelper.CalcCrc(reqBody.ToArray());
            reqBody.AddRange(crc);

            await SendRawRtuPacketAsync(deviceCode, reqBody.ToArray());
        }

        /// <summary>
        /// 位状态读取公共实现（01读线圈 / 02读离散输入）。
        /// </summary>
        private async Task<bool[]> ReadBitStatusAsync(string deviceCode, byte funcCode, ushort startAddr, ushort count)
        {
            var cfg = GetDeviceConfig(deviceCode);
            List<byte> reqBody = new List<byte>
            {
                cfg.SlaveId,
                funcCode,
                (byte)(startAddr >> 8),
                (byte)(startAddr & 0xFF),
                (byte)(count >> 8),
                (byte)(count & 0xFF)
            };
            var crc = ModbusCrcHelper.CalcCrc(reqBody.ToArray());
            reqBody.AddRange(crc);

            var respBytes = await SendRawRtuPacketAsync(deviceCode, reqBody.ToArray());

            if (!ModbusCrcHelper.CheckCrc(respBytes, respBytes.Length))
                throw new Exception($"设备{deviceCode}返回报文CRC校验失败");

            if ((respBytes[1] & 0x80) != 0)
                throw new Exception($"Modbus设备{deviceCode}异常，异常码:{respBytes[2]}");

            int dataLen = respBytes[2];
            if (respBytes.Length < 3 + dataLen)
                throw new Exception($"设备{deviceCode}应答报文截断");

            // 位数据从 resp[3] 开始，每字节低位在前解包为 bool[count]
            bool[] result = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int byteIdx = i / 8;
                int bitIdx = i % 8;
                byte b = respBytes[3 + byteIdx];
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
            List<byte> reqBody = new List<byte>
            {
                cfg.SlaveId,
                funcCode,
                (byte)(startAddr >> 8),
                (byte)(startAddr & 0xFF),
                (byte)(count >> 8),
                (byte)(count & 0xFF)
            };
            var crc = ModbusCrcHelper.CalcCrc(reqBody.ToArray());
            reqBody.AddRange(crc);

            var respBytes = await SendRawRtuPacketAsync(deviceCode, reqBody.ToArray());

            if (!ModbusCrcHelper.CheckCrc(respBytes, respBytes.Length))
                throw new Exception($"设备{deviceCode}返回报文CRC校验失败");

            if ((respBytes[1] & 0x80) != 0)
                throw new Exception($"Modbus设备{deviceCode}异常，异常码:{respBytes[2]}");

            int dataLen = respBytes[2];
            if (dataLen <= 0 || dataLen % 2 != 0)
                throw new Exception($"设备{deviceCode}返回寄存器数据字节长度非法，dataLen={dataLen}");
            if (respBytes.Length < 3 + dataLen)
                throw new Exception($"设备{deviceCode}应答报文截断");

            ushort[] result = new ushort[dataLen / 2];
            for (int i = 0; i < result.Length; i++)
            {
                byte high = respBytes[3 + i * 2];
                byte low = respBytes[4 + i * 2];
                result[i] = (ushort)(high << 8 | low);
            }
            return result;
        }

        public async Task<byte[]> SendRawRtuPacketAsync(string deviceCode, byte[] rtuBytes)
        {
            var cfg = GetDeviceConfig(deviceCode);
            try
            {
                _logger.LogDebug("Modbus[{DeviceCode}] 发送RTU报文: {Hex}",
                    deviceCode, BitConverter.ToString(rtuBytes));

                var response = await InnerTcpSendAsync(
                    cfg.IpAddress, cfg.Port, rtuBytes, cfg.WaitResponse, cfg.TimeoutMs);

                _logger.LogDebug("Modbus[{DeviceCode}] 设备返回报文: {Hex}",
                    deviceCode, BitConverter.ToString(response));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Modbus[{DeviceCode}] 通信异常 Ip:{Ip}:{Port}",
                    deviceCode, cfg.IpAddress, cfg.Port);
                throw;
            }
        }
        #endregion

        #region 内置私有TCP收发
        private async Task<byte[]> InnerTcpSendAsync(string serverIp, int port, byte[] sendData, bool waitResponse = true, int timeoutMs = 1000)
        {
            CancellationTokenSource tokenSource = new();
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
                        _logger.LogWarning("TCP通信超时 Ip:{Ip}:{Port},Timeout:{Timeout}ms", serverIp, port, timeoutMs);
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
                tokenSource.Dispose();
            }

            return recvDatas.ToArray();
        }
        #endregion
    }
}
