using Common.GlobalHelper;
using Common.LocalEntity;
using ConnectionModbusRtuWithTcp;
using ConnectionModbusRtuWithTcp.LocalHelper;
using Microsoft.AspNetCore.Mvc;

namespace NetworkComponent.Controllers
{
    /// <summary>
    /// ModbusRtu over TCP 连接操作
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class ModbusRtuOverTcpOperationController : ControllerBase
    {
        private readonly IModbusRtuWithTcpClient _modbusClient;

        public ModbusRtuOverTcpOperationController(IModbusRtuWithTcpClient modbusClient)
        {
            _modbusClient = modbusClient;
        }

        /// <summary>
        /// 组装返回：业务数据 + 最近一次原始报文（hex）
        /// </summary>
        private ModbusResult<T> Wrap<T>(string deviceCode, T data)
        {
            var ex = _modbusClient.GetLastExchange(deviceCode);
            return new ModbusResult<T>
            {
                Data = data,
                RawRequest = ex?.req,
                RawResponse = ex?.resp,
                Time = ex?.time
            };
        }

        /// <summary>读取保持寄存器（0x03）</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ModbusResult<ushort[]>>> ReadRegister(
            string deviceCode, ushort startAddr, ushort count)
        {
            var data = await _modbusClient.ReadHoldRegistersAsync(deviceCode, startAddr, count);
            return ApiReturnHelper.Success(Wrap(deviceCode, data));
        }

        /// <summary>写入单个寄存器（0x06）</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ModbusResult<object?>>> WriteSingleRegister(
            string deviceCode, ushort addr, ushort value)
        {
            await _modbusClient.WriteSingleRegisterAsync(deviceCode, addr, value);
            return ApiReturnHelper.Success(Wrap<object?>(deviceCode, null), "写入完成");
        }

        /// <summary>读取线圈（0x01）</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ModbusResult<bool[]>>> ReadCoil(
            string deviceCode, ushort startAddr, ushort count)
        {
            var data = await _modbusClient.ReadCoilsAsync(deviceCode, startAddr, count);
            return ApiReturnHelper.Success(Wrap(deviceCode, data));
        }

        /// <summary>读取离散输入（0x02）</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ModbusResult<bool[]>>> ReadDiscreteInput(
            string deviceCode, ushort startAddr, ushort count)
        {
            var data = await _modbusClient.ReadDiscreteInputsAsync(deviceCode, startAddr, count);
            return ApiReturnHelper.Success(Wrap(deviceCode, data));
        }

        /// <summary>读取输入寄存器（0x04）</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ModbusResult<ushort[]>>> ReadInputRegister(
            string deviceCode, ushort startAddr, ushort count)
        {
            var data = await _modbusClient.ReadInputRegistersAsync(deviceCode, startAddr, count);
            return ApiReturnHelper.Success(Wrap(deviceCode, data));
        }

        /// <summary>写入单个线圈（0x05）</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ModbusResult<object?>>> WriteSingleCoil(
            string deviceCode, ushort addr, bool value)
        {
            await _modbusClient.WriteSingleCoilAsync(deviceCode, addr, value);
            return ApiReturnHelper.Success(Wrap<object?>(deviceCode, null), "写线圈完成");
        }

        /// <summary>批量写入多个线圈（0x0F）</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ModbusResult<object?>>> WriteMultiCoil([FromBody] ModbusMultiCoilDto dto)
        {
            await _modbusClient.WriteMultiCoilsAsync(dto.DeviceCode, dto.StartAddr, dto.Values ?? Array.Empty<bool>());
            return ApiReturnHelper.Success(Wrap<object?>(dto.DeviceCode, null), "批量写线圈完成");
        }

        /// <summary>批量写入多个保持寄存器（0x10）</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ModbusResult<object?>>> WriteMultiRegister([FromBody] ModbusTcpReadRegisterDto2 dto)
        {
            await _modbusClient.WriteMultiRegistersAsync(dto.DeviceCode, dto.StartAddr, (dto.Value ?? new List<ushort>()).ToArray());
            return ApiReturnHelper.Success(Wrap<object?>(dto.DeviceCode, null), "批量写寄存器完成");
        }

        /// <summary>获取全部设备编码列表</summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<List<string>> GetAllDeviceCode()
        {
            return ApiReturnHelper.Success(_modbusClient.GetAllDeviceCodes());
        }

        /// <summary>关闭指定设备长连接</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> CloseLongConnection(string deviceCode)
        {
            await _modbusClient.CloseConnectionAsync(deviceCode);
            return ApiReturnHelper.Success(null, "长连接已关闭");
        }

        /// <summary>查询指定设备长连接状态</summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<object> GetLongConnectionStatus(string deviceCode)
        {
            object status = new { deviceCode, connected = _modbusClient.IsConnected(deviceCode) };
            return ApiReturnHelper.Success(status);
        }

        /// <summary>批量查询所有设备在线状态</summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<Dictionary<string, bool>> GetAllDeviceStatus()
        {
            return ApiReturnHelper.Success(_modbusClient.GetAllDeviceStatus());
        }

        /// <summary>
        /// Modbus RTU CRC16 校验码计算器：输入 hex 字节串（如 "01 03 00 00 00 02"），返回 CRC 低字节/高字节/完整 hex。
        /// </summary>
        /// <param name="hex">不带 CRC 的报文 hex，空格或逗号分隔均可</param>
        [HttpGet]
        public ApiUnifiedReturnStructure<object> CalcCrc(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                object empty = new { input = "", crcLow = "", crcHigh = "", crcFull = "", fullFrame = "" };
                return ApiReturnHelper.Success(empty);
            }

            var clean = hex.Replace(",", " ").Replace("0x", "").Replace("0X", "");
            var parts = clean.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var bytes = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!byte.TryParse(parts[i], System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                    throw new ArgumentException($"无法解析 hex 段：{parts[i]}");
            }
            var crc = ModbusCrcHelper.CalcCrc(bytes);
            var full = bytes.Concat(crc).ToArray();
            object result = new
            {
                input = hex.Trim(),
                crcLow = crc[0].ToString("X2"),
                crcHigh = crc[1].ToString("X2"),
                crcFull = $"{crc[0]:X2} {crc[1]:X2}",
                fullFrame = BitConverter.ToString(full).Replace("-", " ")
            };
            return ApiReturnHelper.Success(result);
        }
    }

    /// <summary>
    /// Modbus 调用结果包装：业务数据 + 原始报文 hex
    /// </summary>
    public class ModbusResult<T>
    {
        public T? Data { get; set; }
        public string? RawRequest { get; set; }
        public string? RawResponse { get; set; }
        public DateTime? Time { get; set; }
    }
}
