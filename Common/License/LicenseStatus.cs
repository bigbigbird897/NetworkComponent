namespace Common.License
{
    /// <summary>授权校验结果状态。</summary>
    public enum LicenseState
    {
        /// <summary>未授权（无 license 文件且试用已结束）</summary>
        Unlicensed,
        /// <summary>试用中</summary>
        TrialActive,
        /// <summary>试用已过期</summary>
        TrialExpired,
        /// <summary>正式授权有效（永久或未到期）</summary>
        Active,
        /// <summary>license 文件存在但无效（签名/机器码/时间不匹配）</summary>
        Invalid
    }

    /// <summary>授权校验结果。</summary>
    public class LicenseStatus
    {
        /// <summary>当前状态</summary>
        public LicenseState State { get; set; }

        /// <summary>可读提示信息</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>本机机器码（用于发给软件商申请授权）</summary>
        public string MachineCode { get; set; } = string.Empty;

        /// <summary>到期时间（如有）</summary>
        public DateTime? ExpireAt { get; set; }

        /// <summary>客户名称（如有）</summary>
        public string Customer { get; set; } = string.Empty;

        /// <summary>是否授权允许局域网内其他设备调用（仅正式 license 且软件商开启时为 true）</summary>
        public bool AllowLanAccess { get; set; }

        /// <summary>是否允许调用业务接口</summary>
        public bool IsAllowed => State is LicenseState.Active or LicenseState.TrialActive;
    }
}
