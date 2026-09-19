using System.Collections.Concurrent;
using System.Text;
using ConnectionOPCUA.LocalEntity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

namespace ConnectionOPCUA
{
    /// <summary>
    /// OPC UA 客户端实现。
    /// 职责：
    /// 1. 从 appsettings.json 的 "OpcUaConfigs" 节读取多设备配置；
    /// 2. 按设备编码缓存 OPC UA <see cref="Session"/>，按需惰性建连、断线自动重建；
    /// 3. 对外提供节点读写与连接测试能力。
    /// 参考 OPCFoundation.NetStandard.Opc.Ua 标准客户端写法（Session.Create）。
    /// </summary>
    public class OpcUaClientService : IOpcUaClient
    {
        private readonly IConfiguration _config;
        private readonly ILogger<OpcUaClientService> _logger;

        /// <summary>设备编码 -> 设备配置 的内存缓存</summary>
        private readonly ConcurrentDictionary<string, OpcUaClientConfig> _deviceDict = new();

        /// <summary>设备编码 -> OPC UA 会话 的内存缓存（懒加载，用到才建连）</summary>
        private readonly ConcurrentDictionary<string, Session> _sessionDict = new();

        /// <summary>会话级操作锁，避免同一设备并发建连/读写导致会话状态错乱</summary>
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _lockDict = new();

        /// <summary>证书存储根目录（首次建连时自动创建）</summary>
        private readonly string _certificateStoreRoot;

        /// <summary>共享的应用配置（所有会话复用同一份客户端证书/配额配置）</summary>
        private ApplicationConfiguration? _applicationConfig;

        /// <summary>
        /// 构造函数：由 Autofac 注入配置与日志，启动时加载全部设备配置。
        /// </summary>
        public OpcUaClientService(IConfiguration config, ILogger<OpcUaClientService> logger)
        {
            _config = config;
            _logger = logger;
            _certificateStoreRoot = Path.Combine(AppContext.BaseDirectory, "CertificateStores", "OpcUa");

            var list = _config.GetSection("OpcUaConfigs").Get<List<OpcUaClientConfig>>() ?? new();
            foreach (var item in list)
            {
                if (!string.IsNullOrWhiteSpace(item.DeviceCode))
                {
                    _deviceDict.TryAdd(item.DeviceCode, item);
                }
            }
            _logger.LogInformation("OPCUA 已加载设备数：{Count}", _deviceDict.Count);
        }

        #region IOpcUaClient 接口实现
        /// <inheritdoc />
        public OpcUaClientConfig GetDeviceConfig(string deviceCode)
        {
            if (_deviceDict.TryGetValue(deviceCode, out var cfg))
                return cfg;
            throw new KeyNotFoundException($"未找到DeviceCode={deviceCode}的OPCUA设备配置");
        }

        /// <inheritdoc />
        public List<string> GetAllDeviceCodes() => _deviceDict.Keys.ToList();

        /// <inheritdoc />
        public async Task<object?> ReadNodeAsync(string deviceCode, string nodeId)
        {
            var session = await EnsureSessionAsync(deviceCode);
            var dataValue = await Task.Run(() => session.ReadValue(new NodeId(nodeId)));
            _logger.LogDebug("OPCUA[{DeviceCode}] 读取节点 {NodeId} = {Value}", deviceCode, nodeId, dataValue?.Value);
            return dataValue?.Value;
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, object?>> ReadNodesAsync(string deviceCode, IEnumerable<string> nodeIds)
        {
            var result = new Dictionary<string, object?>();
            var ids = nodeIds.ToList();
            if (ids.Count == 0) return result;

            var session = await EnsureSessionAsync(deviceCode);
            foreach (var nodeId in ids)
            {
                try
                {
                    var dv = await Task.Run(() => session.ReadValue(new NodeId(nodeId)));
                    result[nodeId] = dv?.Value;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OPCUA[{DeviceCode}] 读取节点 {NodeId} 失败", deviceCode, nodeId);
                    result[nodeId] = null;
                }
            }
            return result;
        }

        /// <inheritdoc />
        public async Task WriteNodeAsync(string deviceCode, string nodeId, object value)
        {
            var session = await EnsureSessionAsync(deviceCode);
            await Task.Run(() =>
            {
                // 组装单个写入请求：指定节点 + Value 属性 + 目标值
                var writeValue = new WriteValue
                {
                    NodeId = new NodeId(nodeId),
                    AttributeId = Attributes.Value,
                    Value = new DataValue { Value = value }
                };
                // 调用底层 Write 服务，结果为每个节点的状态码集合
                session.Write(
                    requestHeader: null,
                    nodesToWrite: new WriteValueCollection { writeValue },
                    results: out StatusCodeCollection results,
                    diagnosticInfos: out _);

                var statusCode = results[0];
                if (StatusCode.IsNotGood(statusCode))
                {
                    throw new ServiceResultException(statusCode, $"OPCUA 写入节点 {nodeId} 失败");
                }
            });
            _logger.LogDebug("OPCUA[{DeviceCode}] 写入节点 {NodeId} = {Value}", deviceCode, nodeId, value);
        }

        /// <inheritdoc />
        public async Task<bool> TestConnectionAsync(string deviceCode)
        {
            try
            {
                var cfg = GetDeviceConfig(deviceCode);
                var session = await CreateSessionAsync(cfg);
                await Task.Run(() => session.Close());
                _sessionDict.TryRemove(deviceCode, out _);
                _logger.LogInformation("OPCUA[{DeviceCode}] 连接测试成功 Endpoint={Url}", deviceCode, cfg.EndpointUrl);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OPCUA[{DeviceCode}] 连接测试失败", deviceCode);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task CloseAsync(string deviceCode)
        {
            if (_sessionDict.TryRemove(deviceCode, out var session))
            {
                try { await Task.Run(() => session.Close()); }
                catch (Exception ex) { _logger.LogWarning(ex, "OPCUA[{DeviceCode}] 关闭会话异常", deviceCode); }
                session.Dispose();
            }
        }

        /// <inheritdoc />
        public async Task CloseAllAsync()
        {
            foreach (var deviceCode in _sessionDict.Keys.ToList())
            {
                await CloseAsync(deviceCode);
            }
        }
        #endregion

        #region 内部会话管理
        /// <summary>
        /// 获取（必要时重建）指定设备的可用会话。
        /// </summary>
        private async Task<Session> EnsureSessionAsync(string deviceCode)
        {
            var cfg = GetDeviceConfig(deviceCode);
            var sem = _lockDict.GetOrAdd(deviceCode, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync();
            try
            {
                // 已有会话：判断是否仍然连接，断开则丢弃重建
                if (_sessionDict.TryGetValue(deviceCode, out var existSession))
                {
                    if (IsSessionAlive(existSession))
                    {
                        return existSession;
                    }
                    _logger.LogWarning("OPCUA[{DeviceCode}] 会话已断开，重建连接", deviceCode);
                    TryDisposeSession(existSession);
                    _sessionDict.TryRemove(deviceCode, out _);
                }

                var session = await CreateSessionAsync(cfg);
                _sessionDict[deviceCode] = session;
                return session;
            }
            finally
            {
                sem.Release();
            }
        }

        /// <summary>
        /// 根据配置真正建立一条 OPC UA 会话。
        /// </summary>
        private async Task<Session> CreateSessionAsync(OpcUaClientConfig cfg)
        {
            var appConfig = await GetApplicationConfigAsync(cfg);

            // 1. 选择服务端端点（useSecurity=false 时使用 None 安全策略）
            var endpointDescription = CoreClientUtils.SelectEndpoint(
                appConfig, cfg.EndpointUrl, cfg.UseSecurity, cfg.OperationTimeoutMs);
            var endpoint = new ConfiguredEndpoint(
                null, endpointDescription, EndpointConfiguration.Create(appConfig));

            // 2. 构造身份：用户名密码或匿名（本版本 UserIdentity 密码按 UTF-8 字节传入）
            IUserIdentity identity = string.IsNullOrWhiteSpace(cfg.UserName)
                ? new UserIdentity(new AnonymousIdentityToken())
                : new UserIdentity(cfg.UserName, Encoding.UTF8.GetBytes(cfg.Password ?? string.Empty));

            // 3. 建立会话
            _logger.LogInformation("OPCUA 正在连接 {DeviceCode} -> {Url}", cfg.DeviceCode, cfg.EndpointUrl);
            var session = await Session.Create(
                appConfig,
                endpoint,
                updateBeforeConnect: true,
                sessionName: cfg.SessionName,
                sessionTimeout: 60000,
                identity: identity,
                preferredLocales: null);

            // 连接中断后，EnsureSessionAsync 会通过 session.Connected 自动重建会话，
            // 此处仅订阅 KeepAlive 事件用于观测连接质量。
            session.KeepAlive += (sender, e) =>
            {
                _logger.LogDebug("OPCUA[{DeviceCode}] KeepAlive 心跳正常", cfg.DeviceCode);
            };

            _logger.LogInformation("OPCUA 连接成功 {DeviceCode} -> {Url}", cfg.DeviceCode, cfg.EndpointUrl);
            return session;
        }

        /// <summary>
        /// 构建并校验全局共享的 OPC UA 客户端应用配置（含证书存储、传输配额）。
        /// </summary>
        private async Task<ApplicationConfiguration> GetApplicationConfigAsync(OpcUaClientConfig cfg)
        {
            if (_applicationConfig != null) return _applicationConfig;

            var issuerPath = Path.Combine(_certificateStoreRoot, "trusted", "issuer");
            var trustedPath = Path.Combine(_certificateStoreRoot, "trusted", "peer");
            var rejectedPath = Path.Combine(_certificateStoreRoot, "rejected");
            Directory.CreateDirectory(issuerPath);
            Directory.CreateDirectory(trustedPath);
            Directory.CreateDirectory(rejectedPath);

            var config = new ApplicationConfiguration
            {
                ApplicationName = "NetworkComponent",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    // 客户端自签名证书（None 安全策略下可为空标识）
                    ApplicationCertificate = new CertificateIdentifier(),
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = issuerPath
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = trustedPath
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = rejectedPath
                    },
                    AutoAcceptUntrustedCertificates = cfg.AutoAcceptUntrustedCertificates,
                    AddAppCertToTrustedStore = true
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas
                {
                    OperationTimeout = cfg.OperationTimeoutMs,
                    MaxStringLength = 65535,
                    MaxByteStringLength = 65535 * 1024,
                    MaxArrayLength = 65535
                },
                ClientConfiguration = new ClientConfiguration
                {
                    DefaultSessionTimeout = 60000
                }
            };

            await config.Validate(ApplicationType.Client);
            _applicationConfig = config;
            return config;
        }

        /// <summary>
        /// 粗判会话是否存活（未连接或已终止即视为不可用）。
        /// </summary>
        private static bool IsSessionAlive(Session session)
        {
            try
            {
                return session != null && session.Connected && session.Disposed == false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 安全释放会话，吞掉释放阶段异常。
        /// </summary>
        private void TryDisposeSession(Session session)
        {
            try
            {
                session.Close();
            }
            catch
            {
                // 关闭阶段异常忽略，直接 Dispose
            }
            finally
            {
                session.Dispose();
            }
        }
        #endregion
    }
}
