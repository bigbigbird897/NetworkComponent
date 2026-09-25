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
    /// 支持两种模式（由 SocketClientConfig.UseLongConnection 决定，默认短连接）：
    ///  1. 短连接：连接 -> 发送 -> （可选）等待应答 -> 关闭，一问一答场景；
    ///  2. 长连接：同一设备复用一条常驻 TCP 连接，可手动打开/关闭，适合频繁收发。
    /// 与 ModbusTcp / ModbusRtuWithTcp 底层的 InnerTcpSendAsync 保持一致的超时与取消模型，
    /// 不绑定具体业务协议，适用于自定义 TCP 报文透传。
    /// </summary>
    public class SocketClientService : ISocketClient
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SocketClientService> _logger;

        /// <summary>设备编码 -> 配置 的内存缓存</summary>
        private readonly ConcurrentDictionary<string, SocketClientConfig> _deviceDict = new();

        /// <summary>设备编码 -> 长连接 的内存缓存（仅 UseLongConnection=true 的设备使用）</summary>
        private readonly ConcurrentDictionary<string, LongTcpConnection> _longConnections = new();

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
        public Dictionary<string, bool> GetAllDeviceStatus()
        {
            var result = new Dictionary<string, bool>();
            foreach (var code in _deviceDict.Keys)
                result[code] = IsLongConnectionOpen(code);
            return result;
        }
        /// <inheritdoc />
        public async Task<byte[]> SendAndReceiveAsync(string deviceCode, byte[] sendData, int? timeoutMs = null)
        {
            var cfg = GetDeviceConfig(deviceCode);
            var timeout = timeoutMs ?? cfg.TimeoutMs;
            try
            {
                _logger.LogInformation("Socket[{DeviceCode}] 发送: {Hex}", deviceCode, BitConverter.ToString(sendData));
                byte[] resp = cfg.UseLongConnection
                    ? await LongTcpSendAndReceiveAsync(deviceCode, cfg, sendData, timeout).ConfigureAwait(false)
                    : await InnerTcpSendAsync(cfg.IpAddress, cfg.Port, sendData, cfg.WaitResponse, timeout).ConfigureAwait(false);
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
            var respBytes = await SendAndReceiveAsync(deviceCode, sendBytes, timeoutMs).ConfigureAwait(false);
            return encoding.GetString(respBytes);
        }

        /// <inheritdoc />
        public async Task SendAsync(string deviceCode, byte[] sendData)
        {
            var cfg = GetDeviceConfig(deviceCode);
            try
            {
                _logger.LogInformation("Socket[{DeviceCode}] 发送(不等应答): {Hex}", deviceCode, BitConverter.ToString(sendData));
                if (cfg.UseLongConnection)
                {
                    await LongTcpSendOnlyAsync(deviceCode, cfg, sendData).ConfigureAwait(false);
                }
                else
                {
                    await InnerTcpSendAsync(cfg.IpAddress, cfg.Port, sendData, waitResponse: false, cfg.TimeoutMs).ConfigureAwait(false);
                }
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

        /// <inheritdoc />
        public async Task OpenLongConnectionAsync(string deviceCode)
        {
            var cfg = GetDeviceConfig(deviceCode);
            var conn = _longConnections.GetOrAdd(deviceCode, _ => new LongTcpConnection(_logger, deviceCode, cfg));
            await conn.EnsureConnectedAsync().ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Task CloseLongConnectionAsync(string deviceCode)
        {
            if (_longConnections.TryRemove(deviceCode, out var conn))
            {
                conn.Close();
                _logger.LogInformation("Socket[{DeviceCode}] 长连接已手动关闭", deviceCode);
            }
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public LongConnectionStatus GetLongConnectionStatus(string deviceCode)
        {
            var cfg = GetDeviceConfig(deviceCode);
            var conn = _longConnections.GetValueOrDefault(deviceCode);
            return conn?.ToStatus() ?? new LongConnectionStatus
            {
                DeviceCode = deviceCode,
                IsOpen = false,
                IpAddress = cfg.IpAddress,
                Port = cfg.Port
            };
        }

        /// <inheritdoc />
        public bool IsLongConnectionOpen(string deviceCode)
            => _longConnections.TryGetValue(deviceCode, out var conn) && conn.IsConnected;
        #endregion

        #region 长连接实现

        /// <summary>通过长连接发送并等待一次应答（内部自动建连，断线自动重连）</summary>
        private async Task<byte[]> LongTcpSendAndReceiveAsync(string deviceCode, SocketClientConfig cfg, byte[] sendData, int timeoutMs)
        {
            var conn = _longConnections.GetOrAdd(deviceCode, _ => new LongTcpConnection(_logger, deviceCode, cfg));
            return await conn.SendAndReceiveAsync(sendData, timeoutMs).ConfigureAwait(false);
        }

        /// <summary>通过长连接仅发送（内部自动建连，断线自动重连）</summary>
        private async Task LongTcpSendOnlyAsync(string deviceCode, SocketClientConfig cfg, byte[] sendData)
        {
            var conn = _longConnections.GetOrAdd(deviceCode, _ => new LongTcpConnection(_logger, deviceCode, cfg));
            await conn.SendOnlyAsync(sendData).ConfigureAwait(false);
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

        #region 长连接内部封装
        /// <summary>
        /// 单设备长连接封装：维护一条常驻 TCP 连接 + 后台读取循环。
        /// 串行化发送（写锁），接收到的数据块按“发送并等待”语义交给挂起的等待者；
        /// 超时未收到应答时解除挂起（迟到的数据块会被丢弃），不影响连接本身。
        /// </summary>
        private sealed class LongTcpConnection : IDisposable
        {
            private readonly ILogger _logger;
            private readonly string _deviceCode;
            private readonly SocketClientConfig _cfg;
            private readonly object _sync = new();
            private readonly SemaphoreSlim _writeLock = new(1, 1);
            private Socket? _socket;
            private CancellationTokenSource? _cts;
            private Task? _readerTask;
            private DateTime _connectedSince;
            private long _bytesSent;
            private long _bytesReceived;
            /// <summary>当前挂起的“发送并等待应答”等待者（同一时刻最多一个）</summary>
            private volatile TaskCompletionSource<byte[]>? _pending;

            public LongTcpConnection(ILogger logger, string deviceCode, SocketClientConfig cfg)
            {
                _logger = logger;
                _deviceCode = deviceCode;
                _cfg = cfg;
            }

            public bool IsConnected
            {
                get
                {
                    lock (_sync)
                    {
                        return _socket != null && _readerTask != null && !_readerTask.IsCompleted;
                    }
                }
            }

            /// <summary>确保连接已建立；若已断开则重建并重启读取循环</summary>
            public async Task EnsureConnectedAsync()
            {
                if (IsConnected) return;

                Socket? newSocket = null;
                CancellationTokenSource? newCts = null;
                lock (_sync)
                {
                    if (IsConnected) return;
                    CleanupCore();
                    newSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    newCts = new CancellationTokenSource();
                    _socket = newSocket;
                    _cts = newCts;
                    _connectedSince = DateTime.Now;
                    _bytesSent = 0;
                    _bytesReceived = 0;
                    _pending = null;
                }

                try
                {
                    await newSocket.ConnectAsync(new IPEndPoint(IPAddress.Parse(_cfg.IpAddress), _cfg.Port)).ConfigureAwait(false);
                    lock (_sync)
                    {
                        _readerTask = ReadLoopAsync(newSocket, newCts!);
                    }
                    _logger.LogInformation("Socket[{DeviceCode}] 长连接已建立 {Ip}:{Port}", _deviceCode, _cfg.IpAddress, _cfg.Port);
                }
                catch (Exception ex)
                {
                    lock (_sync)
                    {
                        if (ReferenceEquals(_socket, newSocket)) { _socket = null; }
                        _readerTask = null;
                    }
                    newCts?.Dispose();
                    try { newSocket?.Close(); } catch { /* 忽略清理异常 */ }
                    _logger.LogError(ex, "Socket[{DeviceCode}] 长连接建立失败 {Ip}:{Port}", _deviceCode, _cfg.IpAddress, _cfg.Port);
                    throw;
                }
            }

            /// <summary>发送并等待“下一个”数据块作为应答；超时返回空数组（不关闭连接）</summary>
            public async Task<byte[]> SendAndReceiveAsync(byte[] sendData, int timeoutMs)
            {
                await EnsureConnectedAsync().ConfigureAwait(false);
                await _writeLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _pending = tcs;
                    try
                    {
                        await _socket!.SendAsync(sendData, SocketFlags.None).ConfigureAwait(false);
                        Interlocked.Add(ref _bytesSent, sendData.Length);
                    }
                    catch
                    {
                        _pending = null;
                        throw;
                    }

                    var done = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);
                    if (done == tcs.Task)
                    {
                        return await tcs.Task.ConfigureAwait(false);
                    }

                    // 超时：解除挂起；若稍后数据到达，读取循环会发现无等待者而丢弃
                    if (ReferenceEquals(Interlocked.CompareExchange(ref _pending, null, tcs), tcs))
                    {
                        _logger.LogWarning("Socket[{DeviceCode}] 长连接应答超时 Timeout={Timeout}ms", _deviceCode, timeoutMs);
                    }
                    return Array.Empty<byte>();
                }
                finally
                {
                    _writeLock.Release();
                }
            }

            /// <summary>仅发送（不等待应答）</summary>
            public async Task SendOnlyAsync(byte[] sendData)
            {
                await EnsureConnectedAsync().ConfigureAwait(false);
                await _writeLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    await _socket!.SendAsync(sendData, SocketFlags.None).ConfigureAwait(false);
                    Interlocked.Add(ref _bytesSent, sendData.Length);
                }
                finally
                {
                    _writeLock.Release();
                }
            }

            /// <summary>后台读取循环：收数据块 -> 交给当前等待者或丢弃</summary>
            private async Task ReadLoopAsync(Socket socket, CancellationTokenSource cts)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(4096);
                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        int n = await socket.ReceiveAsync(buffer, SocketFlags.None, cts.Token).ConfigureAwait(false);
                        if (n == 0)
                        {
                            _logger.LogWarning("Socket[{DeviceCode}] 长连接被对端关闭", _deviceCode);
                            break;
                        }
                        var chunk = buffer.AsSpan(0, n).ToArray();
                        Interlocked.Add(ref _bytesReceived, n);
                        _logger.LogInformation("Socket[{DeviceCode}] 长连接收到 {Len} 字节: {Hex}", _deviceCode, n, BitConverter.ToString(chunk));

                        var pending = _pending;
                        if (pending != null && ReferenceEquals(Interlocked.CompareExchange(ref _pending, null, pending), pending))
                        {
                            pending.TrySetResult(chunk);
                        }
                        else
                        {
                            _logger.LogDebug("Socket[{DeviceCode}] 丢弃无等待者的数据块 {Len} 字节", _deviceCode, n);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常关闭路径
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Socket[{DeviceCode}] 长连接读取循环异常退出", _deviceCode);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            /// <summary>关闭连接并释放资源（幂等）</summary>
            public void Close()
            {
                lock (_sync)
                {
                    CleanupCore();
                }
            }

            private void CleanupCore()
            {
                var cts = _cts; _cts = null;
                var sock = _socket; _socket = null;
                _readerTask = null;
                var pending = _pending; _pending = null;
                pending?.TrySetResult(Array.Empty<byte>());

                try { cts?.Cancel(); } catch { /* 忽略 */ }
                if (sock != null)
                {
                    try { sock.Shutdown(SocketShutdown.Both); } catch { /* 忽略 */ }
                    try { sock.Close(); } catch { /* 忽略 */ }
                }
                cts?.Dispose();
            }

            /// <summary>生成状态快照</summary>
            public LongConnectionStatus ToStatus() => new()
            {
                DeviceCode = _deviceCode,
                IsOpen = IsConnected,
                IpAddress = _cfg.IpAddress,
                Port = _cfg.Port,
                ConnectedSince = IsConnected ? _connectedSince.ToString("yyyy-MM-dd HH:mm:ss") : null,
                BytesSent = Interlocked.Read(ref _bytesSent),
                BytesReceived = Interlocked.Read(ref _bytesReceived)
            };

            public void Dispose()
            {
                Close();
                _writeLock.Dispose();
            }
        }
        #endregion
    }
}
