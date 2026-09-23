using Common.License;
using Common.LocalEntity;

namespace ServiceComponentEnhance.Interfaces
{
    /// <summary>
    /// 授权（License）服务接口：提供本机机器码与授权状态的查询。
    /// </summary>
    public interface ILicenseService
    {
        /// <summary>获取本机机器码（发给软件商，用于签发绑定本机的 license）</summary>
        ApiUnifiedReturnStructure<string> GetMachineCode();

        /// <summary>获取当前授权状态（试用剩余时间 / 正式授权到期时间 / 是否过期）</summary>
        ApiUnifiedReturnStructure<LicenseStatus> GetStatus();
    }
}
