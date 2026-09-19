using System.Collections.Generic;

namespace Common.License
{
    /// <summary>
    /// 授权与接口访问控制配置（对应 appsettings.json 的 "License" 节）。
    /// </summary>
    public class LicenseSettings
    {
        /// <summary>RSA 公钥（XML 格式），用于验签；由软件商保管私钥。</summary>
        public string PublicKey { get; set; } = string.Empty;

        /// <summary>license 文件名（放在程序运行目录）。</summary>
        public string LicenseFileName { get; set; } = "license.lic";

        /// <summary>调用业务接口必须携带的 API Key（请求头 X-Api-Key）；为空则不校验（仅调试）。</summary>
        public string ApiKey { get; set; } = string.Empty;

        // 注意：是否允许局域网其他设备调用，不再放在这里（客户可改）。
        // 改为由签名 license 内的 AllowLanAccess 开关决定，默认仅本机回环可调用。
    }
}
