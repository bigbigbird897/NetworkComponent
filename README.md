# NetworkComponent 工业通信中间件

基于 **.NET 10 / ASP.NET Core** 的工业设备通信中间件，以 REST API 形式对外统一暴露
Modbus、MQTT、OPC UA、TCP Socket 等工业总线通信能力，供上位机 / HMI / 业务系统调用。

---

## 一、解决方案结构

解决方案文件：`NetworkComponent.slnx`，共 8 个项目，分层如下：

| 项目 | 职责 |
| --- | --- |
| **NetworkComponent**（Web 入口） | ASP.NET Core 主机，包含 `Program.cs`、Controllers、统一报文实体；承载 HTTP API、Swagger、Serilog、SqlSugar 初始化。 |
| **ArchitectureConfiguration** | Autofac 依赖注入模块，批量扫描各通信程序集并注册“接口 → 实现”。 |
| **Common** | 公共基础库：统一返回模型、API 返回助手、JSON/字符串/反射等通用工具；所有第三方通信依赖包集中安装于此。 |
| **ConnectionModbusTcp** | 标准 Modbus-TCP（MBAP+PDU）客户端，03/06/10 功能码。 |
| **ConnectModbusRtuWithTcp** | Modbus RTU over TCP 客户端（含 CRC16 校验）。 |
| **ConnectionMqtt** | 多 MQTT 客户端管理（连接、发布、订阅、托管订阅、请求-应答、断线重连）。 |
| **ConnectionOPCUA**（本次新增） | OPC UA 客户端（会话管理、节点读/写、批量读、连接测试）。 |
| **ConnectionSocket**（本次新增） | 通用 TCP Socket 短连接收发（二进制 / 字符串）。 |

依赖关系：所有 `ConnectionXxx` 与 `ArchitectureConfiguration` 均引用 `Common`；Web 项目引用全部模块。

---

## 二、基础框架与技术栈

- **运行时**：.NET 10（ASP.NET Core Web）。
- **依赖注入**：Autofac（`Autofac.Extensions.DependencyInjection` + `Autofac.Extras.DynamicProxy`），替换默认容器；通信服务以**单例**注册并开启属性注入，控制器走 Autofac 激活。
- **日志**：Serilog（控制台 + 按天滚动文件 `logs/nc-*.log`），并接入 `UseSerilogRequestLogging` 记录 HTTP 请求。
- **API 文档**：Swashbuckle.AspNetCore（Swagger），开发环境根路径 `/swagger` 可直接打开文档页。
- **ORM**：SqlSugarCore（`SqlSugarScope` 单例，线程安全，自动关闭连接），连接串来自 `appsettings.json`。
- **工业通信库**：MQTTnet（MQTT）、OPCFoundation.NetStandard.Opc.Ua（OPC UA）、原生 `System.Net.Sockets`（Modbus/Socket）。
- **JSON**：Newtonsoft.Json。

### 统一返回模型

所有接口返回 `Common.LocalEntity.ApiUnifiedReturnStructure<T>`：

```json
{ "code": 200, "msg": "操作成功", "data": { }, "timestamp": "2026-09-19 11:44:40" }
```

通过 `ApiReturnHelper.Success / ClientError / ServerError` 统一构造。

---

## 三、核心配置说明（appsettings.json）

| 配置节 | 说明 |
| --- | --- |
| `Serilog` | 日志最小级别与输出过滤。 |
| `SqlSugar` | `DbType`、`ConnectionString`、`IsAutoCloseConnection`，按实际数据库修改。 |
| `ModbusTcpConfigs` | Modbus-TCP 设备列表：`DeviceCode / IpAddress / Port / SlaveId / TimeoutMs / WaitResponse`。 |
| `ModbusRtuWithTcpConfigs` | RTU-over-TCP 设备列表，字段同上。 |
| `MqttConfigs` | MQTT 客户端列表：`ClientId / ServerIp / Port / UserName / Password / CleanSession / KeepAliveSecond`。 |
| `OpcUaConfigs` | OPC UA 设备列表：`DeviceCode / EndpointUrl(opc.tcp://) / UseSecurity / AutoAcceptUntrustedCertificates / OperationTimeoutMs / UserName / Password`。 |
| `SocketConfigs` | 通用 Socket 设备列表：`DeviceCode / IpAddress / Port / TimeoutMs / WaitResponse / Encoding`。 |

> 示例配置中设备地址均为 `127.0.0.1` 占位，**上线前请按真实设备修改**。各通信服务为懒加载，配置错误不会阻断程序启动。

---

## 四、对外接口一览（开发环境 `/swagger`）

| 控制器 | 主要接口 |
| --- | --- |
| `ModbusTcpOperation` | `ReadRegister(03)` / `ReadCoil(01)` / `ReadDiscreteInput(02)` / `ReadInputRegister(04)` / `WriteSingleRegister(06)` / `WriteSingleCoil(05)` / `WriteMultiRegister(10)` / `WriteMultiCoil(0F)` / `SendRawPacket` / `GetAllDeviceCode` |
| `ModbusRtuOperation` | `ReadRegister(03)` / `ReadCoil(01)` / `ReadDiscreteInput(02)` / `ReadInputRegister(04)` / `WriteSingleRegister(06)` / `WriteSingleCoil(05)` / `WriteMultiCoil(0F)` / `GetAllDeviceCode` |
| `MqttOperation` | `PublishMsg` / `SubTopic` / `UnSubTopic` / `PublishAndWaitReply` |
| `OpcUaOperation`（新增） | `ReadNode` / `ReadNodes` / `WriteNode` / `TestConnection` / `GetAllDeviceCode` |
| `SocketOperation`（新增） | `SendAndReceiveBytes` / `SendAndReceiveString` / `SendOnly` / `TestConnection` / `GetAllDeviceCode` |
| `SocketServerOperation`（新增） | `StartServer` / `StopServer` / `SendToClient` / `SendStringToClient` / `Broadcast` / `BroadcastString` / `GetConnectedClients` / `GetRecentMessages` / `GetAllServerCode` |

---

## 五、解决了什么问题

1. **多协议统一接入**：把 Modbus-TCP、Modbus-RTU-over-TCP、MQTT、OPC UA、通用 TCP Socket 收敛到同一进程、同一套 REST API 与统一返回结构，业务侧无需关心底层协议细节。
2. **多设备管理**：每类协议支持在配置中声明多台设备，按 `DeviceCode` 寻址，内存缓存配置与连接，避免每次请求重复建连。
3. **可维护的 DI 架构**：Autofac 按程序集批量注册，新增通信模块只需在 `AutofacConfig.ConnectionAssemblyFiles` 中追加 dll 名即可自动接入。
4. **可观测性**：Serilog 控制台 + 文件双输出，通信收发报文以十六进制/内容打日志；Swagger 在线调试。
5. **工程化**：统一异常/返回模型、SqlSugar 数据访问占位、Nullable 可空注解清理，保证 `0 警告 0 错误` 编译。

---

## 六、运行方式

```powershell
# 还原并编译
dotnet build NetworkComponent.slnx

# 运行（默认 http://localhost:5126 ，Swagger 见 http://localhost:5126/swagger）
dotnet run --project NetworkComponent
```

---

## 七、更改记录

> 按要求，每次修改都在此追加记录。

### 2026-09-23 Socket 客户端：新增长连接模式 / 修复仅发送与 HEX 发送

1. **新增长连接模式（保留短连接）**：
   - `LocalEntity/SocketClientConfig.cs`：新增 `UseLongConnection`（默认 false）。`false`=原短连接（发完即断），`true`=同一设备复用一条常驻 TCP 连接。
   - `ISocketClient.cs`：新增 `OpenLongConnectionAsync / CloseLongConnectionAsync / GetLongConnectionStatus / IsLongConnectionOpen` 长连接管理接口。
   - `SocketClientService.cs`：内置 `LongTcpConnection` 封装——常驻 Socket + 后台读取循环 + 写锁串行化；断线自动重建；超时只丢弃迟到应答、不关闭连接。
   - `SocketClientOperationController.cs`：新增 `OpenLongConnection / CloseLongConnection / GetLongConnectionStatus` 接口及 `SocketDeviceInput` DTO。
   - `appsettings.json`：`SocketClientConfigs` 示例设备补 `"UseLongConnection": false`。
2. **修复“仅发送(不等待)无法把字符串发到服务端”**：
   - 原 `SendOnly` 只接受二进制 `Data`，前端传字符串会 400 或发空。
   - Controller 新增 `SendOnlyString`（`SocketStringInput`），按设备配置编码把字符串转字节后发送。
3. **修复“发送 16 进制报 400”**：
   - 原 `SocketBinaryInput.Data` 为 `byte[]`，System.Text.Json 无法把 HEX 字符串（如 `"01 03 00 00"`）绑定到 `byte[]`，返回 400“JSON value could not be converted to System.Byte[]”。
   - 新增 `HexByteArrayConverter`（`JsonConverter<byte[]>`）挂在 `Data` 上：兼容 **JSON 数字数组**、**HEX 字符串**（空格/逗号/分号分隔、可带 0x 前缀、可无分隔符）、**单个数字** 三种写法。
4. **Web 控制台同步**：Socket 客户端页新增“仅发送字符串”“仅发送HEX”“打开/关闭/刷新长连接状态”；HEX 输入说明支持数组或字符串。
5. **验证**：后端 `dotnet build` 0 错误；Web `npm run build` 通过。

### 2026-09-23 修复 appsettings.json 缺逗号导致配置加载失败（v1.2.1）

1. **问题**：上版发布（v1.2.0）的 `appsettings.json` 中 `SocketClientConfigs` 的示例设备在 `"Encoding": "GBK"` 行后**缺少逗号**就接了 `"UseLongConnection"`，程序启动加载配置时抛 `System.IO.InvalidDataException: Failed to load configuration from file ...appsettings.json`。
2. **修复**：在 `"Encoding": "GBK"` 行尾补上逗号，恢复合法 JSON（已用 `ConvertFrom-Json` 验证解析通过）。
3. **发布**：后端打 v1.2.1 tag；父仓库 `repos.json` 将 `NetworkComponent` 指向 `v1.2.1` 后打 v1.2.1 tag，CI 重新打包发布。

### 2026-09-23 修复构建错误：新项目未还原 + WindowsBase 冲突（移除 Common 中的 WebView2）

1. **问题一：找不到资产文件**：新增的 `ModelComponentEnhance`、`ServiceComponentEnhance` 两个项目（Model/Service 分层骨架，均引用 `Common`）从未还原过 NuGet，构建报“找不到 obj/project.assets.json”。已在解决方案级 `dotnet restore NetworkComponent.slnx` 完成还原。
2. **问题二：WindowsBase 版本冲突（MSB3277）**：`Common.csproj` 误引用了 `Microsoft.Web.WebView2 1.0.4191.47`（WPF 桌面壳才需要的依赖），该包内含 `net5.0-windows` 的 WPF 程序集，经 `Common` 传递到所有后端项目（ConnectionMqtt/ArchitectureConfiguration/NetworkComponent 等），与 .NET 10 的 `WindowsBase 4.0/5.0` 冲突。
3. **修复**：从 `Common.csproj` 移除 `Microsoft.Web.WebView2` 引用（`Common` 内无任何代码使用 WebView2；WPF 壳项目 `NetworkComponentWPF` 已自带 `Microsoft.Web.WebView2 1.0.2903.40` 引用，不受影响）。
4. **验证**：`dotnet build NetworkComponent.slnx` **0 错误**；MSB3277 冲突警告已消失（剩余 9 条为 OPC UA 库既有 API 过时 CS0618 警告，与本次无关）。

### 2026-09-23 将 AutofacPropertityRegConfig 迁移到 ArchitectureConfiguration 项目

1. **背景**：控制器属性注入模块 `AutofacPropertityRegConfig.cs` 原位于主项目内嵌套目录 `NetworkComponent/ArchitectureConfiguration/`（namespace `NetworkComponent.ArchitectureConfiguration`），随主项目 Sdk.Web 编译；本次迁移到独立的 `ArchitectureConfiguration` 类库项目。
2. **迁移改动**：
   - 文件移至 `ArchitectureConfiguration/AutofacPropertityRegConfig.cs`，命名空间改为 `ArchitectureConfiguration`（与 `AutofacConfig` 一致）。
   - 原实现 `typeof(Program).Assembly` 改为 `Assembly.GetEntryAssembly()`（类库不能反向引用主项目，会形成循环依赖；入口程序集即主 Web 项目，行为等价）。
   - `ArchitectureConfiguration.csproj` 增加 `<FrameworkReference Include="Microsoft.AspNetCore.App" />`，以编译期引用 `ControllerBase`（运行时由 ASP.NET Core 主机提供，不打进依赖）。
   - 删除主项目下旧的 `NetworkComponent/ArchitectureConfiguration/` 目录；`Program.cs` 移除 `using NetworkComponent.ArchitectureConfiguration;`（已有 `using ArchitectureConfiguration;`，`RegisterModule<AutofacPropertityRegConfig>()` 调用不变）。
3. **验证**：`dotnet build NetworkComponent.slnx` **0 警告 0 错误**；启动冒烟通过（监听 0.0.0.0:5000，MQTT/SocketServer 初始化正常，控制器属性注入模块加载无异常）。

### 2026-09-19 初始化梳理 + 新增 OPCUA/Socket + 工程化接入

1. **README 初版**：梳理现有框架、组件、配置与解决的问题并落档。
2. **新增 ConnectionOPCUA**：
   - `LocalEntity/OpcUaClientConfig.cs`：OPC UA 单设备配置实体。
   - `IOpcUaClient.cs`：节点读/批量读/写、连接测试、会话关闭等接口。
   - `OpcUaClientService.cs`：基于 OPCFoundation `Session.Create` 的实现，会话按 `DeviceCode` 缓存、惰性建连、断线自动重建；用户名密码认证密码按 UTF-8 字节传入。
3. **新增 ConnectionSocket**：
   - `LocalEntity/SocketClientConfig.cs`：通用 Socket 配置实体。
   - `ISocketClient.cs` / `SocketClientService.cs`：短连接“连接-发送-接收-断开”，支持二进制与字符串收发、纯发送、连通性测试。
4. **新增 Controller**：
   - `OpcUaOperationController.cs`、`SocketOperationController.cs`，含对应请求 DTO。
5. **依赖包安装到 Common 项目**：
   - `OPCFoundation.NetStandard.Opc.Ua` 1.5.378.176
   - `SqlSugarCore` 5.1.4.221
   - `Microsoft.Extensions.Configuration.Abstractions` / `Microsoft.Extensions.Logging.Abstractions` 10.0.12（显式补齐，供通信模块自读配置）
6. **主 Web 项目新增包**：`Swashbuckle.AspNetCore` 10.2.3、`Serilog.AspNetCore` 10.0.0、`Serilog.Sinks.Console` 6.1.1、`Serilog.Sinks.File` 7.0.0。
7. **Program.cs 接入**：
   - Serilog（`UseSerilog` + 控制台/按天文件 + `UseSerilogRequestLogging`）。
   - Swagger（`AddSwaggerGen` + `UseSwagger/UseSwaggerUI`，根路径 `/swagger`）。
   - SqlSugar（`AddSingleton<ISqlSugarClient>` 注册 `SqlSugarScope`，连接串读自配置）。
   - 启动时安全初始化 MQTT 多客户端（失败不阻断主程序）。
8. **修复 Autofac 注册 Bug**：原 `AutofacConfig.cs` 五个分支均错误地注册了同一程序集（复制粘贴遗漏），改为遍历 `ConnectionAssemblyFiles` 正确注册各通信程序集。
9. **统一配置加载方式**：`ModbusTcpClient`、`ModbusRtuWithTcpClient` 由“构造函数注入 List 配置”改为“自 `IConfiguration` 读取对应配置节”，与 MQTT/OPCUA/Socket 保持一致，**修复了 Autofac 无法解析 `List<配置>` 构造参数导致控制器运行时 500 的问题**。
10. **appsettings.json**：补充 `Serilog`、`SqlSugar`、各通信模块配置节及示例设备。
11. **代码优化与中文注释**：全量补充 XML 注释；清理 Nullable 警告（统一返回模型、API 助手、对象-字典工具、各 Controller、HitbotMessage），编译达到 **0 警告 0 错误**。
12. **冒烟验证**：程序正常启动，`/swagger/v1/swagger.json` 返回全部接口；`OpcUaOperation/SocketOperation/ModbusTcpOperation/GetAllDeviceCode` 均 HTTP 200 正常返回。

### 2026-09-19 修复 Socket 中文乱码（GBK 编码）

1. **问题**：通过 `SendAndReceiveString` 发送中文，接收端显示为 `浣犲ソ` 之类乱码。
   原因：默认按 UTF-8 发送，而接收设备（中文 Windows 工具）按 GBK（代码页 936）解码。
2. **修复**：
   - `Program.cs` 启动最前面注册 `CodePagesEncodingProvider.Instance`，使 .NET 可用 GBK/GB2312 编码。
   - 在 `appsettings.json` 的对应 `SocketConfigs` 设备节点把 `"Encoding": "UTF-8"` 改为 `"Encoding": "GBK"` 即可正常收发中文（收发均按该编码编解码）。
3. 其它编码：设备用什么编码就填什么（UTF-8 / GBK / ASCII 等），发送与回包解析共用该编码。

### 2026-09-19 新增 ConnectionSocket 的 SocketServer（监听端）能力

1. **新增文件**：
   - `LocalEntity/SocketServerConfig.cs`：服务端监听配置（`ServerCode / IpAddress / Port / Encoding`）。
   - `LocalEntity/ReceivedSocketMessage.cs`：收到消息的观测实体（原始字节、按编码解码文本、时间）。
   - `ISocketServerService.cs` / `SocketServerService.cs`：服务端实现，`TcpListener` 接受多客户端连接，每连接独立异步收发。
2. **能力**：启动/停止监听、向指定客户端单发字节/字符串、向全体客户端广播字节/字符串、查询在线客户端、查询最近收到的消息（每服务端环形缓冲保留 100 条）；断线/异常自动清理客户端。
3. **Controller**：新增 `SocketServerOperationController.cs`，提供 `StartServer / StopServer / SendToClient / SendStringToClient / Broadcast / BroadcastString / GetConnectedClients / GetRecentMessages / GetAllServerCode` 接口及对应 DTO。
4. **配置**：`appsettings.json` 新增 `SocketServerConfigs` 节，示例监听 `0.0.0.0:9001`，编码 `GBK`。
5. **DI**：服务随 `ConnectionSocket.dll` 被 Autofac 自动扫描注册为单例，无需改动注册代码。
6. **验证**：启动服务端后用 TCP 客户端连接并发送中文「你好服务端」，`GetConnectedClients` 正确显示在线客户端，`GetRecentMessages` 按 GBK 正确解码文本；编译 0 警告 0 错误。

### 2026-09-19 补齐 Modbus 功能码（01/02/04/05/0F）

1. **需求**：参照功能码清单，原仅实现 03 读保持寄存器、06 写单寄存器、10 写多寄存器；本次补齐线圈与离散量相关功能码。
2. **ConnectionModbusTcp 与 ConnectModbusRtuWithTcp 同步新增接口与实现**：
   - `ReadCoilsAsync`（01 读线圈）、`ReadDiscreteInputsAsync`（02 读离散输入）：返回 `bool[]`，应答位数据按“每字节低位在前”解包。
   - `ReadInputRegistersAsync`（04 读输入寄存器）：返回 `ushort[]`，报文与 03 一致。
   - `WriteSingleCoilAsync`（05 写单线圈）：ON=0xFF00 / OFF=0x0000，应答回环校验。
   - `WriteMultiCoilsAsync`（0F 写多线圈）：`bool[]` 按位打包为字节（低位在前）后下发。
   - 实现中抽取了 `ReadBitStatusAsync` / `ReadRegistersByFuncAsync` 私有公共方法，TCP 走 MBAP 解析、RTU 走从站+CRC16 校验解析。
3. **Controller**：`ModbusTcpOperation` 与 `ModbusRtuOverTcpOperation` 各新增 `ReadCoil / ReadDiscreteInput / ReadInputRegister / WriteSingleCoil / WriteMultiCoil` 测试接口；批量写线圈复用 DTO `ModbusMultiCoilDto`（`DeviceCode / StartAddr / Values`）。
4. **验证**：编译 0 警告 0 错误；新接口已出现在 `/swagger`。

# OPC UA 客户端自测完整方案

整体思路：**先用成熟OPC UA工具验证服务端是否正常 → 跑自己写的客户端做连通、读、写、订阅测试 → 异常场景压测**。

> 推荐工具：UA Expert（最常用，Windows）、OPC UA Demo Server（Prosys / Unified Automation 免费模拟器，本地搭服务端，不用找真实设备）

## 一、准备工作：搭建/确认OPC UA服务端可用

> 优先先用模拟器，避免直接连生产设备

1. 下载 **Prosys OPC UA Simulation Server**，启动后默认地址类似： `opc.tcp://127.0.0.1:53530/OPCUA/SimulationServer`

2. 打开 UA Expert，输入上面地址，连接：

   - 安全策略：`None`（本地调试用，正式环境要加密）
   - 身份认证：匿名

3. 能连上、能看到节点（如 

   ```
   ns=1;s=Counter
   ```

   、

   ```
   ns=1;s=Random
   ```

   ），说明服务端本身没问题；

   > 如果UA Expert都连不上，**不是你客户端代码问题**，排查：端口、防火墙、安全策略、证书。

> ✅ 节点标识：`ns=1;s=Counter`，后面代码读写都要用这个NodeId。

## 二、自己OPC UA客户端 基础测试项（按顺序测）

### 1. 连接测试

目标：测试客户端建立会话、安全握手、证书信任 测试点：

1. 正常连接：匿名/用户名密码登录，成功建立Session

2. 错误用例（边界测试）

   - 错误地址：`opc.tcp://127.0.0.1:9999`（端口不存在），预期：超时/连接拒绝

   - 服务端关闭，尝试重连，客户端是否抛出合理异常

   - 证书不被服务端信任：模拟器开启安全策略后，客户端证书未加入信任列表，预期返回安全错误

     > 重点：OPC UA默认强制证书，调试阶段很多人直接设置安全策略=None跳过证书校验。

### 2. 读取节点 Read 测试

> 对应OPC UA Read服务

1. 读取单个变量节点（如 

   ```
   ns=1;s=Counter
   ```

   ）

   - 校验返回值：`Value`、`StatusCode`、`SourceTimestamp`
   - StatusCode = Good（0x00000000）才算读取成功

2. 批量读取多个节点（一次Read请求传多个NodeId）

   > 很多客户端库支持批量读，验证批量接口是否正常

3. 异常读场景

   - 读取不存在的NodeId：预期StatusCode=BadNodeIdUnknown
   - 读取只读节点：读没问题，写会报错

### 3. 修改数值 Write 测试

> 对应OPC UA Write服务

1. 找

   可写节点

   （模拟器的 

   ```
   ns=1;s=Counter
   ```

    支持写）

   - 写入同类型值：节点是Int32，写入整数 100
   - 写完立刻Read读回来，校验值等于刚写入的值（闭环验证）

2. 异常写场景

   - 写入类型不匹配：节点是Int32，写入字符串 → 返回BadTypeMismatch
   - 写入只读节点：返回BadNotWritable
   - 写入超出范围的值（如Int16上限）

### 4. 数据订阅（MonitoredItem / Subscription）测试（最常用）

> OPC UA 核心能力，不是轮询，是服务端主动推送

1. 创建订阅，添加监控项，设置采样间隔（比如100ms）
2. 观察：服务端变量变化，客户端能否收到通知（DataChange）
3. 测试：
   - 正常：变量变化 → 回调触发，拿到新值+时间戳
   - 暂停订阅、删除订阅
   - 网络短暂断开，重连后订阅是否恢复（看你的客户端是否做重连逻辑）

### 5. 浏览节点 Browse 测试（可选）

调用Browse接口，遍历服务端节点树，拿到子节点、节点属性。用于测试客户端地址空间浏览功能。

## 三、推荐代码最小示例（C# OPC UA .NET Standard Stack）

> 库：`OPC.Ua.Client`（OPC Foundation官方）

```
// 1. 配置端点，连接服务器
var config = new ApplicationConfiguration
{
    ApplicationName = "MyOpcUaClient",
    ApplicationUri = "urn:MyOpcUaClient",
    SecurityConfiguration = new SecurityConfiguration
    {
        AutoAcceptUntrustedCertificates = true // 调试用！生产禁止
    }
};
config.Validate();

var endpointUrl = "opc.tcp://127.0.0.1:53530/OPCUA/SimulationServer";
var endpoint = CoreClientUtils.SelectEndpoint(endpointUrl, false); // false=不加密

using var session = Session.Create(config, new ConfiguredEndpoint(null, endpoint), false, "", 60000, null, null).Result;

// 2. Read读取节点
NodeId nodeId = new NodeId("Counter",1);
var readResult = session.Read(nodeId);
Console.WriteLine($"读取值：{readResult.Value}, Status={readResult.StatusCode}");

// 3. Write写入节点
WriteValue writeItem = new WriteValue
{
    NodeId = nodeId,
    AttributeId = Attributes.Value,
    Value = new DataValue(new Variant(99))
};
var writeRes = session.Write(new WriteValueCollection(){writeItem});
Console.WriteLine($"写入结果：{writeRes[0]}");

// 4. 订阅监控
var sub = new Subscription(session.DefaultSubscription) { PublishingInterval = 100 };
var item = new MonitoredItem(sub, nodeId);
item.Notification += (o, e) =>
{
    Console.WriteLine($"收到推送：{e.NotificationValue}");
};
sub.AddItem(item);
session.AddSubscription(sub);
sub.Create();
sub.StartMonitoring();

Console.ReadLine();
session.Close();
```

## 四、排查问题清单（常见坑）

1. ✅ UA Expert能连，自己客户端连不上
   - 证书问题：调试打开自动接受不受信任证书
   - 安全策略不匹配：服务端只支持Basic256Sha256，你客户端配None
   - 客户端应用名称/Uri配置缺失
2. ✅ Read成功，Write失败
   - 节点权限：只读节点无法写
   - 数据类型不匹配，Variant封装错误
3. ✅ 读写正常，订阅收不到数据
   - PublishingInterval、采样间隔设置过大
   - 忘记调用 `sub.Create()` / `StartMonitoring()`
   - 回调线程阻塞
4. ✅ 偶尔断连
   - 会话超时参数不合理，需要增加断线重连机制

## 五、自动化测试思路（如果你想写单元测试）

1. 启动本地Prosys模拟器作为测试服务端
2. 单元测试用例：
   - Connect_Disconnect
   - Read_ExistNode_ReturnGoodValue
   - Read_NotExistNode_ReturnBad
   - Write_ValidValue_ReadBackEquals
   - Write_ReadOnlyNode_ReturnBadNotWritable
   - Subscription_DataChangeReceive
3. 可以用 xUnit / NUnit，测试前启动模拟器，测试结束关闭会话

## 六、正式环境注意事项

- 关闭 `AutoAcceptUntrustedCertificates`，生产必须证书管理
- 增加断线重连、会话恢复、订阅重建逻辑
- 增加超时控制，防止IO阻塞

如果你告诉我你用的语言（C#/Python）和OPC UA SDK，我可以直接给你一套可运行最小Demo，包含读、写、订阅代码。

---

### 2026-09-19 新增软件授权（机器绑定）与接口访问控制

1. **授权模型**：按“单台设备永久授权 + 限时试用”设计。
   - 机器码：`System盘卷序列号 + 首张活动网卡MAC` → SHA256 取前 16 位十六进制，同一台机器稳定、换机即变。
   - license 文件：`运行目录/license.lic`，格式 `Base64(载荷JSON).Base64(RSA签名)`，软件商私钥签名、程序内公钥验签，防篡改。
   - 永久授权：载荷 `LicenseType=Permanent`、无到期时间；试用：`ExpireAt=到期时间`。
   - 无 license 文件时走内置宽限试用：首次运行在 `%ProgramData%/NetworkComponent/.trial` 记开始时间，**宽限天数写死在程序内为 10 天**（不在配置中暴露）。
2. **新增文件（Common/License）**：
   - `LicenseInfo.cs`（载荷）、`LicenseStatus.cs`（状态枚举）、`LicenseSettings.cs`（配置）。
   - `MachineCodeHelper.cs`：机器码生成。
   - `LicenseManager.cs`：验签 + 机器码比对 + 到期判断 + 试用逻辑。
   - `LicenseGenerator.cs`：**软件商侧**生成 license（保留私钥用）。
3. **Web 层**：
   - `License/LicenseGuardMiddleware.cs`：业务请求统一网关，依次校验 **授权状态 → X-Api-Key → 客户端 IP 白名单**；未过返回 403/401 统一 JSON。
   - `Controllers/LicenseController.cs`：`GetMachineCode`（客户把机器码发你）、`GetStatus`（查看授权状态）。
4. **签发工具**：`Tools/LicenseTool`（**WPF 桌面程序，不要随产品发布**）。界面操作：选择私钥文件 → 填客户机器码、客户名 → 选“永久授权/限时试用(天数)” → 点“生成 license.lic”并另存。输出：`Tools/LicenseTool/bin/Debug/net10.0-windows/LicenseTool.exe`。
5. **配置（appsettings.json 的 License 节）**：
   - `PublicKey`：RSA 公钥（已填本次生成值）。
   - `ApiKey`：调用业务接口必须带的请求头 `X-Api-Key`（默认 `nc-prod-a8f3k2`，上线请改）。
   - `IpWhitelist`：允许调用的客户端 IP；本机回环(127.0.0.1/::1)自动放行。
   - （已移除 `TrialDays`：宽限天数写死在 DLL 内为 10 天，客户无法改配置延长。）
6. **密钥保管**：本次 RSA 私钥已存到项目外的 `..\_授权密钥_勿发布\license_private.xml`，**切勿打包进客户部署目录**；公钥在程序里。
7. **验证**：无 license 时返回“试用中剩余 30 天”；缺/错 ApiKey 返回 40101；用私钥为本机机器码签永久 license 后重启，`GetStatus` 显示“永久授权”，带正确 ApiKey 的业务接口 200 正常返回。
8. **防“部署到网络被别人直接调用”**：① 默认把服务监听绑在 `127.0.0.1`（按需用 `ASPNETCORE_URLS` 放开到网卡）；② 所有业务接口强制 `X-Api-Key`；③ 配置 `IpWhitelist` 只放行业务机；④ 授权按机器绑定，license 拷到别的机器也无法使用。

### 2026-09-19 LicenseTool 改为 WPF 桌面程序

1. `Tools/LicenseTool` 由控制台改为 WPF（`UseWPF`、`net10.0-windows`、`WinExe`），删除原 `Program.cs`，新增 `App.xaml/.cs`、`MainWindow.xaml/.cs`。
2. 界面流程：浏览选择私钥 XML → 填客户机器码、客户名 → 单选“永久授权/限时试用”（选试用时启用天数输入）→ 点“生成 license.lic”并另存。
3. 业务逻辑仍复用 `Common/License/LicenseGenerator.cs`；输出 `Tools/LicenseTool/bin/Debug/net10.0-windows/LicenseTool.exe`，编译 0 警告 0 错误。

### 2026-09-19 收紧试用天数：从可改配置改为写死 10 天宽限

1. **问题**：原 `appsettings.json` 的 `License:TrialDays` 为明文，客户可直接改大或删 `.trial` 文件无限续用。
2. **改动**：
   - 删除 `LicenseSettings.TrialDays` 配置项与 appsettings 中对应字段。
   - `LicenseManager` 内置常量 `GraceDays = 10`，无 license 文件时按首次运行时间起算 10 天宽限；天数写死在 DLL 内，客户改配置无效。
3. **正式/限时试用**：超过宽限或要给客户更长试用，一律由 `LicenseTool` 签带 `ExpireAt` 的签名 license（客户无法篡改延期）。
4. **宽限标记防篡改**：首次运行时间以 `时间|HMAC-SHA256` 形式写入两个冗余位置（`%ProgramData%\NetworkComponent\.trial` 与 `%LocalAppData%\NetworkComponent\nc.cache`），校验时取两者中有效的最早时间。单删一个文件不会重置计时；伪造/改早时间因内置密钥 HMAC 校验失败而被忽略。

### 2026-09-19 局域网访问改为签名 license 的增值开关（不再放可改配置）

1. **动机**：原 `IpWhitelist` 放在 appsettings.json 里客户可自行编辑放行任意设备；改为写死在代码中，把“允许局域网其他设备调用”做成可售卖的增值授权。
2. **授权模型**：
   - 默认**仅本机回环**（127.0.0.1/::1）可调用 HTTP 业务接口；该规则写死在 `LicenseGuardMiddleware`，配置里已无此项。
   - license 载荷新增 `AllowLanAccess` 布尔字段（默认 false）。软件商在 LicenseTool 勾选“允许局域网内其他设备调用”后，该开关被 RSA 私钥签名固化进 license，客户无法自行开启。
   - 非回环来源访问时：`AllowLanAccess=false` 返回 40304“局域网访问未授权”；`true` 才放行。
3. **改动文件**：
   - `Common/License/LicenseInfo.cs`：新增 `AllowLanAccess`。
   - `Common/License/LicenseStatus.cs`：新增同名状态字段。
   - `Common/License/LicenseManager.cs`：验签后把该字段带入状态。
   - `Common/License/LicenseGenerator.cs`：`GeneratePermanent / GenerateTrial` 增加 `allowLanAccess` 参数。
   - `Common/License/LicenseSettings.cs`：删除 `IpWhitelist`。
   - `NetworkComponent/License/LicenseGuardMiddleware.cs`：IP 白名单逻辑改为“回环恒放行，非回环须 license 开 LAN 开关”。
   - `appsettings.json`：删除 `License:IpWhitelist`。
   - `LicenseTool/MainWindow.xaml(.cs)`：新增“允许局域网内其他设备调用”勾选框；顺手修正项目移动后的 `Common` 引用相对路径（`..\..\` → `..\`）。
4. **边界说明（重要）**：
   - 此开关只管 **HTTP REST 接口**。`SocketServerService` 的裸 TCP 监听端口（如 9001）不经过 HTTP 管道，仍按 `SocketServerConfigs` 绑定地址对外接受连接，不受此开关影响；如需限制 TCP 来源需另行在 Accept 后加 IP/token 校验。
   - 软件作为客户端主动连局域网设备（Socket/Modbus/OPC UA 出方向连接）不受任何影响。

