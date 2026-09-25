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
    /// TCP Socket 服务端实现。
    /// 自 appsettings.json 的 "SocketServerConfigs" 节读取监听配置；
    /// StartAsync 启动 TcpListener 并接受客户端连接，每个连接独立异步收发；
    /// 支持向指定客户端单发、向全体广播，并在内存环形缓冲中保留最近收到的消息。
    /// </summary>
    public class SocketServerService : ISocketServerService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SocketServerService> _logger;

        /// <summary>服务端编码 -> 配置</summary>
        private readonly ConcurrentDictionary<string, SocketServerConfig> _serverDict = new();

        /// <summary>服务端编码 -> 运行时上下文（监听、客户端、取消令牌）</summary>
        private readonly ConcurrentDictionary<string, ServerContext> _contextDict = new();

        /// <summary>服务端编码 -> 最近收到的消息队列（仅用于 HTTP 观测）</summary>
        private readonly ConcurrentDictionary<string, ConcurrentQueue<ReceivedSocketMessage>> _recvBuffer = new();

        /// <summary>每个服务端保留的最近消息条数上限</summary>
        private const int MaxBufferPerServer = 100;

        /// <summary>
        /// 构造函数：读取 "SocketServerConfigs" 节并加载全部服务端配置。
        /// </summary>
        public SocketServerService(IConfiguration config, ILogger<SocketServerService> logger)
        {
            _config = config;
            _logger = logger;
        }

        #region ISocketServerService 接口实现
        /// <inheritdoc />
        public SocketServerConfig GetServerConfig(string serverCode)
        {
            if (_serverDict.TryGetValue(serverCode, out var cfg))
                return cfg;
            throw new KeyNotFoundException($"未找到ServerCode={serverCode}的SocketServer配置");
        }

        /// <inheritdoc />
        public List<string> GetAllServerCodes() => _serverDict.Keys.ToList();

        /// <inheritdoc />
        public Dictionary<string, (bool running, int clientCount)> GetAllServerStatus()
        {
            var result = new Dictionary<string, (bool running, int clientCount)>();
            foreach (var code in _serverDict.Keys)
            {
                if (_contextDict.TryGetValue(code, out var ctx) && !ctx.Stopped)
                    result[code] = (true, ctx.Clients.Count);
                else
                    result[code] = (false, 0);
            }
            return result;
        }
        /// <inheritdoc />
        public async Task StartAsync(string serverCode)
        {
            var cfg = GetServerConfig(serverCode);

            // 已在运行则直接返回，避免重复监听同一端口
            if (_contextDict.TryGetValue(serverCode, out var exist) && !exist.Stopped)
            {
                _logger.LogWarning("SocketServer[{ServerCode}] 已在运行，忽略重复启动", serverCode);
                return;
            }

            var ip = IPAddress.Parse(cfg.IpAddress);
            var listener = new TcpListener(ip, cfg.Port);
            var cts = new CancellationTokenSource();
            var context = new ServerContext
            {
                Listener = listener,
                Cts = cts
            };
            _contextDict[serverCode] = context;

            listener.Start();
            _logger.LogInformation("SocketServer[{ServerCode}] 开始监听 {Ip}:{Port}", serverCode, cfg.IpAddress, cfg.Port);

            // 后台接受连接循环
            context.AcceptLoopTask = Task.Run(() => AcceptLoopAsync(serverCode, cfg, listener, cts.Token));
            await Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task StopAsync(string serverCode)
        {
            if (!_contextDict.TryRemove(serverCode, out var context))
            {
                _logger.LogWarning("SocketServer[{ServerCode}] 未在运行，无需停止", serverCode);
                return;
            }

            context.MarkStopped();
            try { context.Listener.Stop(); } catch { /* 忽略 */ }

            // 断开全部客户端
            foreach (var (clientId, client) in context.Clients.ToList())
            {
                TryCloseClient(clientId, client);
            }
            context.Clients.Clear();

            try { await context.AcceptLoopTask!; } catch { /* 取消导致的异常忽略 */ }
            context.Cts.Dispose();

            _logger.LogInformation("SocketServer[{ServerCode}] 已停止", serverCode);
            await Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task StopAllAsync()
        {
            foreach (var code in _contextDict.Keys.ToList())
            {
                await StopAsync(code);
            }
        }

        /// <inheritdoc />
        public async Task SendToClientAsync(string serverCode, string clientId, byte[] data)
        {
            var context = GetRunningContext(serverCode);
            if (!context.Clients.TryGetValue(clientId, out var client))
            {
                throw new KeyNotFoundException($"服务端{serverCode}下不存在客户端:{clientId}");
            }
            await WriteAsync(client, data);
            _logger.LogDebug("SocketServer[{ServerCode}] -> {ClientId} 发送 {Len} 字节", serverCode, clientId, data.Length);
        }

        /// <inheritdoc />
        public Task SendToClientStringAsync(string serverCode, string clientId, string message)
        {
            var cfg = GetServerConfig(serverCode);
            var bytes = GetEncoding(cfg).GetBytes(message);
            return SendToClientAsync(serverCode, clientId, bytes);
        }

        /// <inheritdoc />
        public async Task BroadcastAsync(string serverCode, byte[] data)
        {
            var context = GetRunningContext(serverCode);
            foreach (var (clientId, client) in context.Clients.ToList())
            {
                try { await WriteAsync(client, data); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SocketServer[{ServerCode}] 广播到 {ClientId} 失败，移除该客户端", serverCode, clientId);
                    RemoveClient(serverCode, clientId);
                }
            }
            _logger.LogDebug("SocketServer[{ServerCode}] 广播给 {Count} 个客户端", serverCode, context.Clients.Count);
        }

        /// <inheritdoc />
        public Task BroadcastStringAsync(string serverCode, string message)
        {
            var cfg = GetServerConfig(serverCode);
            var bytes = GetEncoding(cfg).GetBytes(message);
            return BroadcastAsync(serverCode, bytes);
        }

        /// <inheritdoc />
        public List<string> GetConnectedClients(string serverCode)
        {
            return GetRunningContext(serverCode).Clients.Keys.ToList();
        }

        /// <inheritdoc />
        public List<ReceivedSocketMessage> GetRecentMessages(string serverCode, int take = 20)
        {
            if (!_recvBuffer.TryGetValue(serverCode, out var queue))
                return new List<ReceivedSocketMessage>();

            // 队列按接收顺序存储，取最近 N 条并按时间倒序返回
            return queue.ToArray()
                .OrderByDescending(m => m.Time)
                .Take(Math.Max(1, take))
                .ToList();
        }
        #endregion

        #region 内部实现
        /// <summary>接受连接的主循环，直到被取消。</summary>
        private async Task AcceptLoopAsync(string serverCode, SocketServerConfig cfg, TcpListener listener, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                    var clientId = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString("N");
                    _contextDict[serverCode].Clients[clientId] = client;
                    _logger.LogInformation("SocketServer[{ServerCode}] 客户端接入：{ClientId}", serverCode, clientId);

                    // 每个连接独立处理，不阻塞接受循环
                    _ = HandleClientAsync(serverCode, cfg, clientId, client, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常停止
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SocketServer[{ServerCode}] 接受连接循环异常", serverCode);
            }
        }

        /// <summary>单个客户端的收发循环：读到数据就记录到内存缓冲。</summary>
        private async Task HandleClientAsync(string serverCode, SocketServerConfig cfg, string clientId, TcpClient client, CancellationToken ct)
        {
            var encoding = GetEncoding(cfg);
            var stream = client.GetStream();
            var buffer = new byte[4096];
            try
            {
                while (!ct.IsCancellationRequested && client.Connected)
                {
                    int readLen;
                    try
                    {
                        readLen = await stream.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
                    }
                    catch (IOException)
                    {
                        break; // 连接被对端关闭
                    }

                    if (readLen == 0) break; // 对端正常断开

                    var data = buffer[..readLen];
                    var text = encoding.GetString(data);
                    _logger.LogInformation("SocketServer[{ServerCode}] <- {ClientId}: {Text}", serverCode, clientId, text);

                    StoreMessage(serverCode, new ReceivedSocketMessage
                    {
                        ServerCode = serverCode,
                        ClientId = clientId,
                        Data = data,
                        Text = text,
                        Time = DateTime.Now
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SocketServer[{ServerCode}] 客户端 {ClientId} 处理异常", serverCode, clientId);
            }
            finally
            {
                TryCloseClient(clientId, client);
                RemoveClient(serverCode, clientId);
                _logger.LogInformation("SocketServer[{ServerCode}] 客户端断开：{ClientId}", serverCode, clientId);
            }
        }

        /// <summary>线程安全地向客户端流写入字节（按客户端加锁，避免半包交错）。</summary>
        private async Task WriteAsync(TcpClient client, byte[] data)
        {
            var stream = client.GetStream();
            lock (stream)
            {
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
            await Task.CompletedTask;
        }

        /// <summary>把收到的消息存入内存队列，并裁剪到上限。</summary>
        private void StoreMessage(string serverCode, ReceivedSocketMessage message)
        {
            var queue = _recvBuffer.GetOrAdd(serverCode, _ => new ConcurrentQueue<ReceivedSocketMessage>());
            queue.Enqueue(message);
            while (queue.Count > MaxBufferPerServer && queue.TryDequeue(out _)) { }
        }

        /// <summary>获取正在运行的服务端上下文，未启动则抛错。</summary>
        private ServerContext GetRunningContext(string serverCode)
        {
            if (_contextDict.TryGetValue(serverCode, out var ctx) && !ctx.Stopped)
                return ctx;
            throw new InvalidOperationException($"SocketServer[{serverCode}] 未启动，请先调用启动接口");
        }

        private void RemoveClient(string serverCode, string clientId)
        {
            if (_contextDict.TryGetValue(serverCode, out var ctx))
            {
                ctx.Clients.TryRemove(clientId, out _);
            }
        }

        private void TryCloseClient(string clientId, TcpClient client)
        {
            try { client.Client.Shutdown(SocketShutdown.Both); } catch { }
            try { client.Close(); } catch { }
            client.Dispose();
        }

        /// <summary>解析配置编码，兜底 UTF-8。</summary>
        private static Encoding GetEncoding(SocketServerConfig cfg)
        {
            try { return Encoding.GetEncoding(string.IsNullOrWhiteSpace(cfg.Encoding) ? "UTF-8" : cfg.Encoding); }
            catch { return Encoding.UTF8; }
        }

        public async Task InitAllServersAsync()
        {
            var list = _config.GetSection("SocketServerConfigs").Get<List<SocketServerConfig>>() ?? new();
            foreach (var item in list)
            {
                if (!string.IsNullOrWhiteSpace(item.ServerCode))
                {
                    _serverDict.TryAdd(item.ServerCode, item);
                }
            }
            _logger.LogInformation("SocketServer 已加载服务端数：{Count}", _serverDict.Count);
        }

        /// <summary>单个服务端的运行时上下文。</summary>
        private class ServerContext
        {
            public required TcpListener Listener { get; init; }
            public required CancellationTokenSource Cts { get; init; }
            public Task? AcceptLoopTask { get; set; }
            public ConcurrentDictionary<string, TcpClient> Clients { get; } = new();
            public bool Stopped { get; private set; }
            public void MarkStopped() => Stopped = true;
        }
        #endregion
    }
}
