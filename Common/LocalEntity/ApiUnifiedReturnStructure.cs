namespace Common.LocalEntity
{
    /// <summary>
    /// 工业自动化全局统一返回模型
    /// </summary>
    /// <typeparam name="T">业务数据/设备数据实体</typeparam>
    public class ApiUnifiedReturnStructure<T>
    {
        /// <summary>
        /// 业务状态码（工控专用分层编码）
        /// 200=成功；4xx=请求/参数错误；5xx=设备/服务异常；
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// 人类可读提示信息（前端弹窗、HMI提示）
        /// </summary>
        public string Msg { get; set; } = string.Empty;

        /// <summary>
        /// 业务主体数据（设备、采集、分页、机械臂坐标等）
        /// </summary>
        public T Data { get; set; } = default!;

        /// <summary>
        /// 服务器UTC毫秒时间戳（工控实时数据校准）
        /// </summary>
        public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}