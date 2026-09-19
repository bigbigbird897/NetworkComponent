using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
                    Customer = info.Customer,
                    AllowLanAccess = info.AllowLanAccess
                };
            }
            catch (Exception ex)
            {
                return Invalid($"license 文件读取/校验异常：{ex.Message}");
            }
        }

        /// <summary>无 license 文件时的宽限（试用）天数，写死在程序内，客户无法通过配置修改。</summary>
        private const int GraceDays = 10;

        /// <summary>宽限标记文件的 HMAC 签名密钥（内置在程序内，用于防止客户伪造/篡改首次运行时间）。</summary>
        private static readonly byte[] GraceSecret = Encoding.UTF8.GetBytes(
            "nc-grace-v1::7f3a9c2e5b1d4e8a::" + "29F4138F079F4514");

        /// <summary>
        /// 宽限（试用）判定：
        /// 在两个不同位置各存一份带 HMAC 签名的“首次运行时间”，取最早的一份作为起点。
        /// 单删其中一个文件无法重置计时；伪造更早时间也无法通过 HMAC 校验。
        /// </summary>
        private LicenseStatus EvaluateTrial()
        {
            // 两个冗余位置：ProgramData（隐藏目录）与 LocalAppData（文件名伪装成缓存）
            string[] paths =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NetworkComponent", ".trial"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetworkComponent", "nc.cache")
            };

            DateTime start = ReadEarliestGraceStart(paths);

            if (start == DateTime.MinValue)
            {
                // 全新机器：写入两个位置
                start = DateTime.Now;
                WriteGraceMarker(paths[0], start);
                WriteGraceMarker(paths[1], start);
            }

            var remaining = (GraceDays - (DateTime.Now - start).TotalDays);
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
                Message = $"试用期 {GraceDays} 天已结束，请联系软件商获取正式授权",
                MachineCode = _machineCode
            };
        }

        /// <summary>读取所有位置里有效的最早首次运行时间；都没有或都无效返回 DateTime.MinValue。</summary>
        private DateTime ReadEarliestGraceStart(string[] paths)
        {
            DateTime earliest = DateTime.MaxValue;
            bool any = false;
            foreach (var p in paths)
            {
                try
                {
                    if (!File.Exists(p)) continue;
                    var parts = File.ReadAllText(p).Split('|');
                    if (parts.Length != 2) continue;
                    if (!DateTime.TryParseExact(parts[0], "yyyy-MM-dd HH:mm:ss",
                        null, System.Globalization.DateTimeStyles.None, out var t)) continue;
                    // 校验 HMAC：时间被改动过则校验失败，视为无效
                    if (!FixedTimeEquals(ComputeMac(parts[0]), parts[1])) continue;
                    any = true;
                    if (t < earliest) earliest = t;
                }
                catch { /* 单个位置读失败忽略 */ }
            }
            return any ? earliest : DateTime.MinValue;
        }

        /// <summary>写入一份带 HMAC 签名的标记文件。</summary>
        private void WriteGraceMarker(string path, DateTime start)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var s = start.ToString("yyyy-MM-dd HH:mm:ss");
                File.WriteAllText(path, s + "|" + ComputeMac(s));
            }
            catch { /* 写失败不阻断主流程 */ }
        }

        /// <summary>对首次运行时间做 HMAC-SHA256 签名（Base64）。</summary>
        private string ComputeMac(string s)
        {
            using var hmac = new HMACSHA256(GraceSecret);
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(s)));
        }

        /// <summary>恒定时间比较两个 Base64 签名串，避免时序侧信道。</summary>
        private static bool FixedTimeEquals(string a, string b)
        {
            byte[] ba, bb;
            try { ba = Convert.FromBase64String(a); bb = Convert.FromBase64String(b); }
            catch { return false; }
            if (ba.Length != bb.Length) return false;
            int diff = 0;
            for (int i = 0; i < ba.Length; i++) diff |= ba[i] ^ bb[i];
            return diff == 0;
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
