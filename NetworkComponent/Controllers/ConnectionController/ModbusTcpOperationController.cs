using Common.GlobalHelper;
using Common.LocalEntity;
using ConnectionModbusTcp;
using Microsoft.AspNetCore.Mvc;

namespace NetworkComponent.Controllers
{
    /// <summary>
    /// Modbus TCP 连接操作
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class ModbusTcpOperationController : ControllerBase
    {
        private readonly IModbusTcpClient _modbusTcpClient;

        public ModbusTcpOperationController(IModbusTcpClient modbusTcpClient)
        {
            _modbusTcpClient = modbusTcpClient;
        }

        /// <summary>组装返回：业务数据 + 最近一次原始报文（hex）</summary>
        private ModbusResult<T> Wrap<T>(string deviceCode, T data)
        {
            var ex = _modbusTcpClient.GetLastExchange(deviceCode);
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
            var data = await _modbusTcpClient.ReadHoldRegistersAsync(deviceCode, startAddr, count);
            return ApiReturnHelper.Success(Wrap(deviceCode, data));
        }

        /// <summary>写入单个保持寄存器（0x06）</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ModbusResult<object?>>> WriteSingleRegister(
            string deviceCode, ushort addr, ushort value)
        {
            await _modbusTcpClient.WriteSingleRegisterAsync(deviceCode, addr, value);
            return ApiReturnHelper.Success(Wrap<object?>(deviceCode, null), "写入完成");
        }

        /// <summary>批量写入多个保持寄存器（0x10）</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ModbusResult<object?>>> WriteMultiRegister([FromBody] ModbusTcpReadRegisterDto2 dto2)
        {
            await _modbusTcpClient.WriteMultiRegistersAsync(dto2.DeviceCode, dto2.StartAddr, dto2.Value ?? new List<ushort>());
            return ApiReturnHelper.Success(Wrap<object?>(dto2.DeviceCode, null), "批量写入完成");
        }

        /// <summary>读取线圈（0x01）</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ModbusResult<bool[]>>> ReadCoil(
            string deviceCode, ushort startAddr, ushort count)
        {
            var data = await _modbusTcpClient.ReadCoilsAsync(deviceCode, startAddr, count);
            return ApiReturnHelper.Success(Wrap(deviceCode, data));
        }

        /// <summary>读取离散输入（0x02）</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ModbusResult<bool[]>>> ReadDiscreteInput(
            string deviceCode, ushort startAddr, ushort count)
        {
            var data = await _modbusTcpClient.ReadDiscreteInputsAsync(deviceCode, startAddr, count);
            return ApiReturnHelper.Success(Wrap(deviceCode, data));
        }

        /// <summary>读取输入寄存器（0x04）</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ModbusResult<ushort[]>>> ReadInputRegister(
            string deviceCode, ushort startAddr, ushort count)
        {
            var data = await _modbusTcpClient.ReadInputRegistersAsync(deviceCode, startAddr, count);
            return ApiReturnHelper.Success(Wrap(deviceCode, data));
        }

        /// <summary>写入单个线圈（0x05）</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ModbusResult<object?>>> WriteSingleCoil(
            string deviceCode, ushort addr, bool value)
        {
            await _modbusTcpClient.WriteSingleCoilAsync(deviceCode, addr, value);
            return ApiReturnHelper.Success(Wrap<object?>(deviceCode, null), "写线圈完成");
        }

        /// <summary>批量写入多个线圈（0x0F）</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ModbusResult<object?>>> WriteMultiCoil([FromBody] ModbusMultiCoilDto dto)
        {
            await _modbusTcpClient.WriteMultiCoilsAsync(dto.DeviceCode, dto.StartAddr, dto.Values ?? Array.Empty<bool>());
            return ApiReturnHelper.Success(Wrap<object?>(dto.DeviceCode, null), "批量写线圈完成");
        }

        /// <summary>发送原始 Modbus-TCP 报文（完整 MBAP+PDU）</summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ModbusResult<byte[]>>> SendRawPacket(
            string deviceCode, byte[] tcpPacketBytes)
        {
            var resp = await _modbusTcpClient.SendRawTcpPacketAsync(deviceCode, tcpPacketBytes);
            return ApiReturnHelper.Success(Wrap(deviceCode, resp));
        }

        /// <summary>获取全部已加载设备编码列表</summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<List<string>> GetAllDeviceCode()
        {
            return ApiReturnHelper.Success(_modbusTcpClient.GetAllDeviceCodes());
        }

        /// <summary>批量查询所有设备在线状态（TCP connect 测试）</summary>
        [HttpGet]
        public async Task<ApiUnifiedReturnStructure<Dictionary<string, bool>>> GetAllDeviceStatus()
        {
            return ApiReturnHelper.Success(await _modbusTcpClient.GetAllDeviceStatusAsync());
        }
    }

    public class ModbusTcpReadRegisterDto2
    {
        public string DeviceCode { get; set; } = string.Empty;
        public ushort StartAddr { get; set; }
        public List<ushort>? Value { get; set; }
    }

    /// <summary>批量写线圈入参（TCP 与 RTU 控制器共用）</summary>
    public class ModbusMultiCoilDto
    {
        public string DeviceCode { get; set; } = string.Empty;
        public ushort StartAddr { get; set; }
        public bool[]? Values { get; set; }
    }
}
