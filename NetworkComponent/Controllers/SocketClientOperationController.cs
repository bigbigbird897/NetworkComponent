using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Common.GlobalHelper;
using Common.LocalEntity;
using ConnectionSocket;
using Microsoft.AspNetCore.Mvc;

namespace NetworkComponent.Controllers
{
    /// <summary>
    /// 通用 TCP Socket 操作控制器。
    /// 对外暴露二进制/字符串收发、纯发送、连通性测试、长连接管理等 HTTP 接口，适用于非标 TCP 设备。
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class SocketClientOperationController : ControllerBase
    {
        private readonly ISocketClient _socketClient;

        /// <summary>
        /// 构造函数注入 Socket 客户端服务。
        /// </summary>
        public SocketClientOperationController(ISocketClient socketClient)
        {
            _socketClient = socketClient;
        }

        /// <summary>
        /// 连接设备、发送十六进制字节并等待应答（短连接或按配置走长连接）。
        /// Data 支持两种写法：JSON 数字数组 [1,3,0,0]，或 HEX 字符串 "01 03 00 00"（空格/逗号分隔均可）。
        /// </summary>
        /// <param name="input">二进制收发入参</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<byte[]>> SendAndReceiveBytes([FromBody] SocketBinaryInput input)
        {
            var resp = await _socketClient.SendAndReceiveAsync(
                input.DeviceCode, input.Data ?? Array.Empty<byte>(), input.TimeoutMs);
            return ApiReturnHelper.Success(resp);
        }

        /// <summary>
        /// 按配置编码发送字符串并等待字符串应答。
        /// </summary>
        /// <param name="input">字符串收发入参</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<string>> SendAndReceiveString([FromBody] SocketStringInput input)
        {
            var resp = await _socketClient.SendAndReceiveStringAsync(
                input.DeviceCode, input.Message ?? string.Empty, input.TimeoutMs);
            return ApiReturnHelper.Success(resp);
        }

        /// <summary>
        /// 仅发送字节，不等待设备应答（短连接或按配置走长连接）。
        /// Data 支持 JSON 数字数组或 HEX 字符串两种写法。
        /// </summary>
        /// <param name="input">二进制纯发送入参</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> SendOnly([FromBody] SocketBinaryInput input)
        {
            await _socketClient.SendAsync(input.DeviceCode, input.Data ?? Array.Empty<byte>());
            return ApiReturnHelper.Success(null, "发送完成");
        }

        /// <summary>
        /// 仅发送字符串，不等待设备应答（按配置编码转字节后发送）。
        /// </summary>
        /// <param name="input">字符串纯发送入参</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> SendOnlyString([FromBody] SocketStringInput input)
        {
            var cfg = _socketClient.GetDeviceConfig(input.DeviceCode);
            var encoding = System.Text.Encoding.GetEncoding(string.IsNullOrWhiteSpace(cfg.Encoding) ? "UTF-8" : cfg.Encoding);
            var bytes = encoding.GetBytes(input.Message ?? string.Empty);
            await _socketClient.SendAsync(input.DeviceCode, bytes);
            return ApiReturnHelper.Success(null, "发送完成");
        }

        /// <summary>
        /// 打开指定设备的长连接并保持常驻（重复调用幂等）。
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> OpenLongConnection([FromBody] SocketDeviceInput input)
        {
            await _socketClient.OpenLongConnectionAsync(input.DeviceCode);
            return ApiReturnHelper.Success(null, "长连接已打开");
        }

        /// <summary>
        /// 关闭指定设备的长连接。
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> CloseLongConnection([FromBody] SocketDeviceInput input)
        {
            await _socketClient.CloseLongConnectionAsync(input.DeviceCode);
            return ApiReturnHelper.Success(null, "长连接已关闭");
        }

        /// <summary>
        /// 查询指定设备长连接的状态（是否打开、建连时间、收发字节数）。
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        [HttpGet]
        public ApiUnifiedReturnStructure<object> GetLongConnectionStatus(string deviceCode)
        {
            return ApiReturnHelper.Success((object)_socketClient.GetLongConnectionStatus(deviceCode));
        }

        /// <summary>
        /// 测试与设备的 TCP 连通性。
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        [HttpGet]
        public async Task<ApiUnifiedReturnStructure<bool>> TestConnection(string deviceCode)
        {
            var ok = await _socketClient.TestConnectionAsync(deviceCode);
            return ok ? ApiReturnHelper.Success(true, "连接成功") : ApiReturnHelper.ServerError(false, "连接失败");
        }

        /// <summary>
        /// 获取全部已加载的 Socket 设备编码列表。
        /// </summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<List<string>> GetAllDeviceCode()
        {
            return ApiReturnHelper.Success(_socketClient.GetAllDeviceCodes());
        }
    }

    #region DTO

    /// <summary>仅设备编码入参（长连接打开/关闭）</summary>
    public class SocketDeviceInput
    {
        /// <summary>设备编码</summary>
        public string DeviceCode { get; set; } = string.Empty;
    }

    /// <summary>二进制收发/纯发送入参</summary>
    public class SocketBinaryInput
    {
        /// <summary>设备编码</summary>
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary>待发送字节数组：支持 JSON 数字数组，也支持 HEX 字符串（"01 03 00 00"）</summary>
        [JsonConverter(typeof(HexByteArrayConverter))]
        public byte[]? Data { get; set; }

        /// <summary>可选超时（毫秒），不传则用配置默认值</summary>
        public int? TimeoutMs { get; set; }
    }

    /// <summary>字符串收发入参</summary>
    public class SocketStringInput
    {
        /// <summary>设备编码</summary>
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary>待发送字符串</summary>
        public string? Message { get; set; }

        /// <summary>可选超时（毫秒）</summary>
        public int? TimeoutMs { get; set; }
    }

    /// <summary>
    /// byte[] JSON 转换器：兼容三种写法——
    ///  1. JSON 数字数组：[1,3,0,0]
    ///  2. HEX 字符串："01 03 00 00"（支持空格/逗号/分号分隔、可带 0x 前缀、可无分隔符）
    ///  3. 单个数字：5
    /// 解决 System.Text.Json 默认无法把字符串绑定到 byte[] 导致的 400 校验错误。
    /// </summary>
    public class HexByteArrayConverter : JsonConverter<byte[]>
    {
        public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;

                case JsonTokenType.Number:
                    return new[] { reader.GetByte() };

                case JsonTokenType.String:
                {
                    var s = reader.GetString();
                    if (string.IsNullOrWhiteSpace(s)) return Array.Empty<byte>();
                    // 去掉分隔符与 0x 前缀
                    var tokens = s.Split(new[] { ' ', ',', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(t => t.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? t[2..] : t);
                    var hex = string.Concat(tokens);
                    if (hex.Length % 2 != 0)
                        throw new JsonException($"HEX 字符串长度必须为偶数，实际为 {hex.Length}：{s}");
                    var bytes = new byte[hex.Length / 2];
                    for (var i = 0; i < bytes.Length; i++)
                        bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                    return bytes;
                }

                case JsonTokenType.StartArray:
                {
                    var list = new List<byte>();
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndArray) break;
                        if (reader.TokenType == JsonTokenType.Number)
                            list.Add(reader.GetByte());
                        else if (reader.TokenType == JsonTokenType.String)
                            list.Add(Convert.ToByte(reader.GetString(), 16));
                        else
                            throw new JsonException($"byte[] 数组内只允许数字或 HEX 字符串，遇到 {reader.TokenType}");
                    }
                    return list.ToArray();
                }

                default:
                    throw new JsonException($"无法将 {reader.TokenType} 转换为 byte[]");
            }
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            // 默认以 Base64 输出（与 System.Text.Json 原生 byte[] 行为一致）
            writer.WriteBase64StringValue(value);
        }
    }

    #endregion
}
