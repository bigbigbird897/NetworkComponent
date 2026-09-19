using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ConnectionSocket.LocalEntity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConnectionSocket
{
    /// <summary>
    /// 通用 TCP Socket 客户端实现。
    /// 与 ModbusTcp / ModbusRtuWithTcp 底层的 InnerTcpSendAsync 保持一致的短连接收发模型，
    /// 不绑定具体业务协议，适用于自定义 TCP 报文透传。
    /// </summary>
    public class SocketClientService : ISocketClient
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SocketClientService> _logger;

        /// <summary>设备编码 -> 配置 的内存缓存</summary>
        private readonly ConcurrentDictionary<string, SocketClientConfig> _deviceDict = new();

        /// <summary>
        /// 构造函数：读取 "SocketClientConfigs" 节并加载全部设备配置。
        /// </summary>
        public SocketClientService(IConfiguration config, ILogger<SocketClientService> logger)
        {
            _config = config;
            _logger = logger;

            var list = _config.GetSection("SocketClientConfigs").Get<List<SocketClientConfig>>() ?? new();
            foreach (var item in list)
            {
                if (!string.IsNullOrWhiteSpace(item.DeviceCode))
                {
                    _deviceDict.TryAdd(item.DeviceCode, item);
                }
            }
            _logger.LogInformation("Socket 已加载设备数：{Count}", _deviceDict.Count);
        }

        #region ISocketClient 接口实现
        /// <inheritdoc />
        public SocketClientConfig GetDeviceConfig(string deviceCode)
        {
            if (_deviceDict.TryGetValue(deviceCode, out var cfg))
                return cfg;
            throw new KeyNotFoundException($"未找到DeviceCode={deviceCode}的Socket设备配置");
        }

        /// <inheritdoc />
        public List<string> GetAllDeviceCodes() => _deviceDict.Keys.ToList();

        /// <inheritdoc />
        public async Task<byte[]> SendAndReceiveAsync(string deviceCode, byte[] sendData, int? timeoutMs = null)
        {
            var cfg = GetDeviceConfig(deviceCode);
            var timeout = timeoutMs ?? cfg.TimeoutMs;
            try
            {
                _logger.LogInformation("Socket[{DeviceCode}] 发送: {Hex}", deviceCode, BitConverter.ToString(sendData));
                var resp = await InnerTcpSendAsync(cfg.IpAddress, cfg.Port, sendData, cfg.WaitResponse, timeout);
                _logger.LogInformation("Socket[{DeviceCode}] 接收: {Hex}", deviceCode, BitConverter.ToString(resp));
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Socket[{DeviceCode}] 通信异常 {Ip}:{Port}", deviceCode, cfg.IpAddress, cfg.Port);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<string> SendAndReceiveStringAsync(string deviceCode, string message, int? timeoutMs = null)
        {
            var cfg = GetDeviceConfig(deviceCode);
            var encoding = Encoding.GetEncoding(string.IsNullOrWhiteSpace(cfg.Encoding) ? "UTF-8" : cfg.Encoding);
            var sendBytes = encoding.GetBytes(message);
            var respBytes = await SendAndReceiveAsync(deviceCode, sendBytes, timeoutMs);
            return encoding.GetString(respBytes);
        }

        /// <inheritdoc />
        public async Task SendAsync(string deviceCode, byte[] sendData)
        {
            var cfg = GetDeviceConfig(deviceCode);
            try
            {
                _logger.LogInformation("Socket[{DeviceCode}] 发送(不等应答): {Hex}", deviceCode, BitConverter.ToString(sendData));
                await InnerTcpSendAsync(cfg.IpAddress, cfg.Port, sendData, waitResponse: false, cfg.TimeoutMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Socket[{DeviceCode}] 发送异常 {Ip}:{Port}", deviceCode, cfg.IpAddress, cfg.Port);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> TestConnectionAsync(string deviceCode)
        {
            var cfg = GetDeviceConfig(deviceCode);
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Parse(cfg.IpAddress), cfg.Port);
                _logger.LogInformation("Socket[{DeviceCode}] 连接成功 {Ip}:{Port}", deviceCode, cfg.IpAddress, cfg.Port);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Socket[{DeviceCode}] 连接失败 {Ip}:{Port}", deviceCode, cfg.IpAddress, cfg.Port);
                return false;
            }
        }
        #endregion

        #region 私有 TCP 收发（短连接模型）
        /// <summary>
        /// 底层 TCP 短连接收发：连接 -> 发送 -> （可选）等待应答 -> 关闭。
        /// 与 Modbus 系列保持一致的超时与取消模型。
        /// </summary>
        /// <param name="serverIp">远端 IP</param>
        /// <param name="port">远端端口</param>
        /// <param name="sendData">发送字节</param>
        /// <param name="waitResponse">是否等待应答</param>
        /// <param name="timeoutMs">接收超时毫秒</param>
        private async Task<byte[]> InnerTcpSendAsync(string serverIp, int port, byte[] sendData, bool waitResponse, int timeoutMs)
        {
            using var cts = new CancellationTokenSource();
            var recvDatas = new List<byte>();
            var pool = ArrayPool<byte>.Shared;
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                var serverEp = new IPEndPoint(IPAddress.Parse(serverIp), port);
                await socket.ConnectAsync(serverEp).ConfigureAwait(false);
                await socket.SendAsync(sendData, SocketFlags.None, cts.Token).ConfigureAwait(false);

                if (waitResponse)
                {
                    var buffer = pool.Rent(1024);
                    try
                    {
                        var delayTask = Task.Delay(timeoutMs, cts.Token);
                        var recvTask = Task.Run(async () =>
                        {
                            int readLen = await socket.ReceiveAsync(buffer, SocketFlags.None, cts.Token).ConfigureAwait(false);
                            recvDatas.AddRange(buffer.Take(readLen));
                        }, cts.Token);

                        await Task.WhenAny(delayTask, recvTask).ConfigureAwait(false);
                        if (delayTask.IsCompleted && !recvTask.IsCompleted)
                        {
                            cts.Cancel();
                            _logger.LogWarning("Socket TCP 通信超时 {Ip}:{Port}, Timeout={Timeout}ms", serverIp, port, timeoutMs);
                        }
                    }
                    finally
                    {
                        pool.Return(buffer);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Socket InnerTcpSendAsync 异常 {Ip}:{Port}", serverIp, port);
                throw;
            }
            finally
            {
                if (socket.Connected)
                {
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                }
            }
            return recvDatas.ToArray();
        }
        #endregion
    }
}
