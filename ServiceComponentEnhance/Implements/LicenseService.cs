using Common.GlobalHelper;
using Common.License;
using Common.LocalEntity;
using ServiceComponentEnhance.Interfaces;

namespace ServiceComponentEnhance.Implements
{
    /// <summary>
    /// 授权服务实现：透传 LicenseManager 的机器码与授权状态查询。
    /// LicenseManager 由主项目在容器中注册为单例，构造函数注入即可复用。
    /// </summary>
    public class LicenseService : ILicenseService
    {
        private readonly LicenseManager _licenseManager;

        public LicenseService(LicenseManager licenseManager)
        {
            _licenseManager = licenseManager;
        }

        public ApiUnifiedReturnStructure<string> GetMachineCode()
        {
            return ApiReturnHelper.Success(_licenseManager.MachineCode);
        }

        public ApiUnifiedReturnStructure<LicenseStatus> GetStatus()
        {
            return ApiReturnHelper.Success(_licenseManager.Validate());
        }
    }
}
