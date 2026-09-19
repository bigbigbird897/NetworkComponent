using Common.GlobalHelper;
using Common.License;
using Common.LocalEntity;
using Microsoft.AspNetCore.Mvc;

namespace NetworkComponent.Controllers
{
    /// <summary>
    /// 授权状态查询接口（放行，供部署方查看本机机器码与授权状态，以便向软件商申请 license）。
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class LicenseController : ControllerBase
    {
        private readonly LicenseManager _licenseManager;

        public LicenseController(LicenseManager licenseManager)
        {
            _licenseManager = licenseManager;
        }

        /// <summary>
        /// 获取本机机器码（把这个码发给软件商，用于签发绑定本机的 license）。
        /// </summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<string> GetMachineCode()
        {
            return ApiReturnHelper.Success(_licenseManager.MachineCode);
        }

        /// <summary>
        /// 获取当前授权状态（试用剩余时间 / 正式授权到期时间 / 是否过期）。
        /// </summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<LicenseStatus> GetStatus()
        {
            return ApiReturnHelper.Success(_licenseManager.Validate());
        }
    }
}
