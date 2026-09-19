using Common.GlobalHelper;
using Common.LocalEntity;
using ConnectionModbusRtuWithTcp;
using ConnectionSocket;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NetworkComponent.Controllers
{
    /// <summary>
    /// ModbusRtu连接操作
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    //[ApiExplorerSettings(GroupName = "后台功能")]
    //[Authorize] // 需要JWT登录访问时打开
    public class ModbusRtuOverTcpOperationController : ControllerBase
    {
        private readonly IModbusRtuWithTcpClient _modbusClient;

        public ModbusRtuOverTcpOperationController(IModbusRtuWithTcpClient modbusClient)
        {
            _modbusClient = modbusClient;
        }

        /// <summary>
        /// 读取保持寄存器
        /// </summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<ushort[]>> ReadRegister(
            string deviceCode,
            ushort startAddr,
            ushort count
        )
        {
            var data = await _modbusClient.ReadHoldRegistersAsync(deviceCode, startAddr, count);
            return ApiReturnHelper.Success(data);
        }

        /// <summary>
        /// 写入单个寄存器
        /// </summary>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> WriteSingleRegister(
            string deviceCode,
            ushort addr,
            ushort value
        )
        {
            await _modbusClient.WriteSingleRegisterAsync(deviceCode, addr, value);
            return ApiReturnHelper.Success(null, "写入完成");
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
            var data = await _modbusClient.ReadCoilsAsync(deviceCode, startAddr, count);
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
            var data = await _modbusClient.ReadDiscreteInputsAsync(deviceCode, startAddr, count);
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
            var data = await _modbusClient.ReadInputRegistersAsync(deviceCode, startAddr, count);
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
            await _modbusClient.WriteSingleCoilAsync(deviceCode, addr, value);
            return ApiReturnHelper.Success(null, "写线圈完成");
        }

        /// <summary>
        /// 0F功能码：批量写入多个线圈
        /// </summary>
        /// <param name="dto">设备编码、起始地址、线圈状态数组</param>
        [HttpPost]
        public async Task<ApiUnifiedReturnStructure<object>> WriteMultiCoil([FromBody] ModbusMultiCoilDto dto)
        {
            await _modbusClient.WriteMultiCoilsAsync(dto.DeviceCode, dto.StartAddr, dto.Values ?? Array.Empty<bool>());
            return ApiReturnHelper.Success(null, "批量写线圈完成");
        }


        /// <summary>
        /// 获取全部已加载的  设备编码列表。
        /// </summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<List<string>> GetAllDeviceCode()
        {
            return ApiReturnHelper.Success(_modbusClient.GetAllDeviceCodes());
        }
    }
}
