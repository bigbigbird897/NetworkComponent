using System;
using System.Collections.Generic;
using System.Text;

namespace ConnectionModbusRtuWithTcp.LocalHelper
{
    public static class ModbusCrcHelper
    {
        private static readonly ushort[] CrcTable = new ushort[256];

        static ModbusCrcHelper()
        {
            ushort crc;
            for (int i = 0; i < 256; i++)
            {
                crc = (ushort)i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                        crc = (ushort)((crc >> 1) ^ 0xA001);
                    else
                        crc >>= 1;
                }
                CrcTable[i] = crc;
            }
        }

        /// <summary>
        /// 计算Modbus RTU标准CRC16，返回低字节在前、高字节在后
        /// </summary>
        public static byte[] CalcCrc(byte[] data)
        {
            ushort crc = 0xFFFF;
            foreach (var b in data)
            {
                crc = (ushort)((crc >> 8) ^ CrcTable[(crc & 0xFF) ^ b]);
            }
            return new[] { (byte)(crc & 0xFF), (byte)(crc >> 8) };
        }

        /// <summary>
        /// 校验完整RTU报文CRC是否正确
        /// </summary>
        public static bool CheckCrc(byte[] buffer, int dataLength)
        {
            if (dataLength < 3) return false;
            byte[] body = new byte[dataLength - 2];
            Array.Copy(buffer, body, dataLength - 2);
            var calcCrc = CalcCrc(body);
            return calcCrc[0] == buffer[dataLength - 2] && calcCrc[1] == buffer[dataLength - 1];
        }
    }
}
