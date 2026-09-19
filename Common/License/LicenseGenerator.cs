using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Common.License
{
    /// <summary>
    /// 【软件商侧工具】生成 license 文件内容。
    /// 调用方需持有 RSA 私钥（XML 格式），用它对载荷签名；客户机器只持有公钥用于验签。
    /// 此工具不应随产品发布到客户现场。
    /// </summary>
    public static class LicenseGenerator
    {
        /// <summary>
        /// 生成 license 文本（Base64(载荷).Base64(签名)），保存为 license.lic 放到客户程序目录即可。
        /// </summary>
        /// <param name="privateKeyXml">软件商 RSA 私钥（XML）</param>
        /// <param name="info">授权载荷（机器码、类型、到期时间、客户名）</param>
        public static string Generate(string privateKeyXml, LicenseInfo info)
        {
            using var rsa = RSA.Create();
            rsa.FromXmlString(privateKeyXml);

            string json = JsonConvert.SerializeObject(info);
            byte[] payload = Encoding.UTF8.GetBytes(json);
            byte[] signature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            return $"{Convert.ToBase64String(payload)}.{Convert.ToBase64String(signature)}";
        }

        /// <summary>
        /// 快捷生成永久授权 license。
        /// </summary>
        public static string GeneratePermanent(string privateKeyXml, string machineCode, string customer)
        {
            return Generate(privateKeyXml, new LicenseInfo
            {
                MachineCode = machineCode,
                LicenseType = "Permanent",
                ExpireAt = null,
                IssuedAt = DateTime.Now,
                Customer = customer
            });
        }

        /// <summary>
        /// 快捷生成指定天数的试用 license。
        /// </summary>
        public static string GenerateTrial(string privateKeyXml, string machineCode, string customer, int days)
        {
            return Generate(privateKeyXml, new LicenseInfo
            {
                MachineCode = machineCode,
                LicenseType = "Trial",
                ExpireAt = DateTime.Now.AddDays(days),
                IssuedAt = DateTime.Now,
                Customer = customer
            });
        }
    }
}
