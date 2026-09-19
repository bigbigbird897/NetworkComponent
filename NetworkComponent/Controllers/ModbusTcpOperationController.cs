using Common.GlobalHelper;
using Common.LocalEntity;
using ConnectionModbusTcp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NetworkComponent.Controllers
{
    /// <summary>
    /// ModbusTCP连接
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    //[ApiExplorerSettings(GroupName = "后台功能")]
    //[Authorize] // 需要JWT登录访问时打开
    public class ModbusTcpOperationController : ControllerBase
    {
        private readonly IModbusTcpClient _modbusTcpClient;

        public ModbusTcpOperationController(IModbusTcpClient modbusTcpClient)
        {
            _modbusTcpClient = modbusTcpClient;
        }

        /// <summary>
        /// 03功能码：读取保持寄存器
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="startAddr">PDU起始寄存器地址（0开始）</param>
        /// <param name="count">读取寄存器数量</param>
        /// <returns>寄存器原始ushort数组</returns>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ushort[]>> ReadRegister(
            string deviceCode,
            ushort startAddr,
            ushort count
        )
        {
            var data = await _modbusTcpClient.ReadHoldRegistersAsync(deviceCode, startAddr, count);
            return ApiReturnHelper.Success(data);
        }

        /// <summary>
        /// 06功能码：写入单个保持寄存器
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="addr">PDU寄存器地址（0开始）</param>
        /// <param name="value">待写入寄存器值</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> WriteSingleRegister(
            string deviceCode,
            ushort addr,
            ushort value
        )
        {
            await _modbusTcpClient.WriteSingleRegisterAsync(deviceCode, addr, value);
            return ApiReturnHelper.Success(null, "写入完成");
        }

        /// <summary>
        /// 10功能码：批量写入多个保持寄存器
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="startAddr">PDU起始寄存器地址（0开始）</param>
        /// <param name="values">待写入寄存器数组</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> WriteMultiRegister([FromBody] ModbusTcpReadRegisterDto2 dto2)
        {
            await _modbusTcpClient.WriteMultiRegistersAsync(dto2.DeviceCode, dto2.StartAddr, dto2.Value ?? new List<ushort>());
            return ApiReturnHelper.Success(null, "批量写入完成");
        }

        /// <summary>
        /// 01功能码：读取线圈
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="startAddr">起始线圈地址（0开始）</param>
        /// <param name="count">读取线圈数量</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<bool[]>> ReadCoil(
            string deviceCode, ushort startAddr, ushort count)
        {
            var data = await _modbusTcpClient.ReadCoilsAsync(deviceCode, startAddr, count);
            return ApiReturnHelper.Success(data);
        }

        /// <summary>
        /// 02功能码：读取离散输入
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="startAddr">起始离散输入地址（0开始）</param>
        /// <param name="count">读取数量</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<bool[]>> ReadDiscreteInput(
            string deviceCode, ushort startAddr, ushort count)
        {
            var data = await _modbusTcpClient.ReadDiscreteInputsAsync(deviceCode, startAddr, count);
            return ApiReturnHelper.Success(data);
        }

        /// <summary>
        /// 04功能码：读取输入寄存器
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="startAddr">起始寄存器地址（0开始）</param>
        /// <param name="count">读取寄存器数量</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ushort[]>> ReadInputRegister(
            string deviceCode, ushort startAddr, ushort count)
        {
            var data = await _modbusTcpClient.ReadInputRegistersAsync(deviceCode, startAddr, count);
            return ApiReturnHelper.Success(data);
        }

        /// <summary>
        /// 05功能码：写入单个线圈
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="addr">线圈地址（0开始）</param>
        /// <param name="value">true=ON，false=OFF</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> WriteSingleCoil(
            string deviceCode, ushort addr, bool value)
        {
            await _modbusTcpClient.WriteSingleCoilAsync(deviceCode, addr, value);
            return ApiReturnHelper.Success(null, "写线圈完成");
        }

        /// <summary>
        /// 0F功能码：批量写入多个线圈
        /// </summary>
        /// <param name="dto">设备编码、起始地址、线圈状态数组</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> WriteMultiCoil([FromBody] ModbusMultiCoilDto dto)
        {
            await _modbusTcpClient.WriteMultiCoilsAsync(dto.DeviceCode, dto.StartAddr, dto.Values ?? Array.Empty<bool>());
            return ApiReturnHelper.Success(null, "批量写线圈完成");
        }

        /// <summary>
        /// 发送原始Modbus‑TCP报文（完整MBAP+PDU）
        /// 用于自定义功能码调试
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="tcpPacketBytes">完整MBAP+PDU字节数组</param>
        /// <returns>设备返回原始字节</returns>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<byte[]>> SendRawPacket(
            string deviceCode,
            byte[] tcpPacketBytes
        )
        {
            var resp = await _modbusTcpClient.SendRawTcpPacketAsync(deviceCode, tcpPacketBytes);
            return ApiReturnHelper.Success(resp);
        }

        /// <summary>
        /// 获取全部已加载ModbusTcp设备编码列表
        /// </summary>
        /// <returns>设备编码集合</returns>
        [HttpGet]
        public ApiUnifiedReturnStructure<List<string>> GetAllDeviceCode()
        {
            var list = _modbusTcpClient.GetAllDeviceCodes();
            return ApiReturnHelper.Success(list);
        }
    }

    public class ModbusTcpReadRegisterDto
    {
        public string DeviceCode { get; set; } = string.Empty;
        public ushort StartAddr { get; set; }
        public ushort Count { get; set; }
    }

    public class ModbusTcpReadRegisterDto2
    {
        public string DeviceCode { get; set; } = string.Empty;
        public ushort StartAddr { get; set; }
        public List<ushort>? Value { get; set; }
    }

    /// <summary>
    /// 批量写线圈入参（TCP 与 RTU 控制器共用）
    /// </summary>
    public class ModbusMultiCoilDto
    {
        /// <summary>设备编码</summary>
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary>起始线圈地址（0开始）</summary>
        public ushort StartAddr { get; set; }

        /// <summary>线圈状态数组，true=ON</summary>
        public bool[]? Values { get; set; }
    }
}
