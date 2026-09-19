using System;

namespace Common.License
{
    /// <summary>
    /// 授权票据载荷（被 RSA 私钥签名的内容）。
    /// 永久授权：LicenseType=Permanent 且 ExpireAt 为空；
    /// 试用授权：LicenseType=Trial 且 ExpireAt 为到期时间。
    /// </summary>
    public class LicenseInfo
    {
        /// <summary>被授权的机器码（由硬件指纹计算得出）</summary>
        public string MachineCode { get; set; } = string.Empty;

        /// <summary>授权类型：Permanent=永久，Trial=试用</summary>
        public string LicenseType { get; set; } = "Permanent";

        /// <summary>到期时间；永久授权为 null</summary>
        public DateTime? ExpireAt { get; set; }

        /// <summary>签发时间</summary>
        public DateTime IssuedAt { get; set; }

        /// <summary>客户名称/备注</summary>
        public string Customer { get; set; } = string.Empty;
    }
}
