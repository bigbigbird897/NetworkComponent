using System;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace Common.License
{
    /// <summary>
    /// 授权管理器。
    /// 验证流程：
    /// 1) 读取运行目录下的 license.lic（格式：Base64(载荷JSON).Base64(RSA签名)）；
    /// 2) 用内置公钥验签，校验机器码与到期时间，有效则为正式授权；
    /// 3) 无 license 文件时走试用：首次运行记录开始时间，N 天内为试用中，否则试用过期。
    /// </summary>
    public class LicenseManager
    {
        private readonly LicenseSettings _settings;
        private readonly string _machineCode;

        /// <summary>本机机器码。</summary>
        public string MachineCode => _machineCode;

        public LicenseManager(LicenseSettings settings)
        {
            _settings = settings ?? new LicenseSettings();
            _machineCode = MachineCodeHelper.GetMachineCode();
        }

        /// <summary>执行一次授权校验（每次请求都会调用，开销很小）。</summary>
        public LicenseStatus Validate()
        {
            // 1. 先看是否有正式 license
            var licensePath = Path.Combine(AppContext.BaseDirectory, _settings.LicenseFileName);
            if (File.Exists(licensePath))
            {
                var result = VerifyLicenseFile(licensePath);
                if (result != null) return result;
                // license 文件存在但无效，继续判断试用（避免一个坏文件直接把人锁死）
            }

            // 2. 试用逻辑
            return EvaluateTrial();
        }

        /// <summary>验签并校验 license 文件，失败返回 null。</summary>
        private LicenseStatus? VerifyLicenseFile(string path)
        {
            try
            {
                var text = File.ReadAllText(path).Trim();
                var parts = text.Split('.');
                if (parts.Length != 2)
                    return Invalid("license 文件格式错误（应为 载荷.签名）");

                byte[] payload = Convert.FromBase64String(parts[0]);
                byte[] signature = Convert.FromBase64String(parts[1]);

                using var rsa = RSA.Create();
                rsa.FromXmlString(_settings.PublicKey);
                if (!rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                    return Invalid("license 签名校验失败，文件可能被篡改");

                var info = JsonConvert.DeserializeObject<LicenseInfo>(System.Text.Encoding.UTF8.GetString(payload));
                if (info == null) return Invalid("license 载荷解析失败");

                if (!string.Equals(info.MachineCode, _machineCode, StringComparison.OrdinalIgnoreCase))
                    return Invalid($"license 绑定机器码[{info.MachineCode}]与本机[{_machineCode}]不一致");

                bool isPermanent = string.Equals(info.LicenseType, "Permanent", StringComparison.OrdinalIgnoreCase);
                if (!isPermanent && info.ExpireAt.HasValue && info.ExpireAt.Value < DateTime.Now)
                    return Expired(info);

                return new LicenseStatus
                {
                    State = LicenseState.Active,
                    Message = isPermanent ? $"永久授权（客户：{info.Customer}）" : $"授权有效，到期时间：{info.ExpireAt:yyyy-MM-dd}",
                    MachineCode = _machineCode,
                    ExpireAt = info.ExpireAt,
                    Customer = info.Customer
                };
            }
            catch (Exception ex)
            {
                return Invalid($"license 文件读取/校验异常：{ex.Message}");
            }
        }

        /// <summary>试用判定：首次运行写标记文件，按 TrialDays 判断。</summary>
        private LicenseStatus EvaluateTrial()
        {
            string markerDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "NetworkComponent");
            Directory.CreateDirectory(markerDir);
            string marker = Path.Combine(markerDir, ".trial");

            DateTime start;
            if (File.Exists(marker))
            {
                if (!DateTime.TryParse(File.ReadAllText(marker), out start))
                    start = DateTime.Now;
            }
            else
            {
                start = DateTime.Now;
                File.WriteAllText(marker, start.ToString("yyyy-MM-dd HH:mm:ss"));
            }

            var remaining = (_settings.TrialDays - (DateTime.Now - start).TotalDays);
            if (remaining > 0)
            {
                return new LicenseStatus
                {
                    State = LicenseState.TrialActive,
                    Message = $"试用中，剩余 {Math.Ceiling(remaining):0} 天",
                    MachineCode = _machineCode
                };
            }

            return new LicenseStatus
            {
                State = LicenseState.TrialExpired,
                Message = $"试用期 {_settings.TrialDays} 天已结束，请联系软件商获取正式授权",
                MachineCode = _machineCode
            };
        }

        private LicenseStatus Invalid(string msg) => new()
        {
            State = LicenseState.Invalid, Message = msg, MachineCode = _machineCode
        };

        private LicenseStatus Expired(LicenseInfo info) => new()
        {
            State = LicenseState.TrialExpired,
            Message = $"授权已于 {info.ExpireAt:yyyy-MM-dd} 到期",
            MachineCode = _machineCode,
            ExpireAt = info.ExpireAt
        };
    }
}
