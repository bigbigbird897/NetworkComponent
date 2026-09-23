using ArchitectureConfiguration;
using Common.License;
using Common.LocalEntity;
using Microsoft.AspNetCore.Mvc;
using ServiceComponentEnhance.Interfaces;

namespace NetworkComponent.Controllers.ComponentEnhanceController
{
    /// <summary>
    /// 授权状态查询接口（放行，供部署方查看本机机器码与授权状态，以便向软件商申请 license）。
    /// 业务逻辑下沉到 Service 层（ILicenseService），本控制器仅负责路由与参数绑定。
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class LicenseController : ApiServiceControllerBase<ILicenseService>
    {
        /// <summary>
        /// 获取本机机器码（把这个码发给软件商，用于签发绑定本机的 license）。
        /// </summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<string> GetMachineCode()
        {
            return Service.GetMachineCode();
        }

        /// <summary>
        /// 获取当前授权状态（试用剩余时间 / 正式授权到期时间 / 是否过期）。
        /// </summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<LicenseStatus> GetStatus()
        {
            return Service.GetStatus();
        }
    }
}
