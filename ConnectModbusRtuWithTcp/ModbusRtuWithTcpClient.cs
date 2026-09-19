using ConnectionModbusRtuWithTcp.LocalHelper;
using ConnectionModbusRtuWithTcp.Model;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace ConnectionModbusRtuWithTcp
{
    public class ModbusRtuWithTcpClient : IModbusRtuWithTcpClient
    {
        private readonly ILogger<ModbusRtuWithTcpClient> _logger;
        private readonly ConcurrentDictionary<string, ModbusRtuWithTcpClientConfig> _deviceDict = new();

        // 构造：由Program传入预加载好的设备配置，不再自己读配置
        public ModbusRtuWithTcpClient(ILogger<ModbusRtuWithTcpClient> logger, List<ModbusRtuWithTcpClientConfig> deviceConfigs)
        {
            _logger = logger;
            if (deviceConfigs != null)
            {
                // 加载设备到内存缓存
                foreach (var dev in deviceConfigs)
                {
                    _deviceDict.TryAdd(dev.DeviceCode, dev);
                }
            }
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
