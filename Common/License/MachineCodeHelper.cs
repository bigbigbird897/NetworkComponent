using System;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Common.License
{
    /// <summary>
    /// 机器码生成：综合系统盘卷序列号 + 第一张活动物理网卡 MAC，做 SHA256 后取前 16 位十六进制。
    /// 同一台机器结果稳定，换机器后变化，用于把授权绑定到单台设备。
    /// </summary>
    public static class MachineCodeHelper
    {
        /// <summary>获取本机机器码（大写 16 位十六进制）。</summary>
        public static string GetMachineCode()
        {
            string raw = $"{GetSystemVolumeSerial()}|{GetPrimaryMac()}";
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash)[..16].ToUpperInvariant();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetVolumeInformation(
            string rootPath,
            StringBuilder? volumeName, uint volumeNameSize,
            out uint volumeSerial,
            uint maxComponentLen, out uint flags,
            StringBuilder? fileSystemName, uint fileSystemNameSize);

        /// <summary>读取系统盘（Windows 通常为 C:）的卷序列号。</summary>
        private static string GetSystemVolumeSerial()
        {
            try
            {
                string root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
                GetVolumeInformation(root, null, 0, out uint serial, 0, out _, null, 0);
                return serial.ToString("X8");
            }
            catch
            {
                return "VOL?";
            }
        }

        /// <summary>取第一张“已连接、非回环”的网卡 MAC（按速率排序，优先有线）。</summary>
        private static string GetPrimaryMac()
        {
            try
            {
                var nic = Array.Find(NetworkInterface.GetAllNetworkInterfaces(),
                    n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                      && n.OperationalStatus == OperationalStatus.Up);
                return nic?.GetPhysicalAddress().ToString() ?? "MAC?";
            }
            catch
            {
                return "MAC?";
            }
        }
    }
}
