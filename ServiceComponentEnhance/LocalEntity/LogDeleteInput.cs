namespace ServiceComponentEnhance.LocalEntity
{
    /// <summary>日志批量删除入参</summary>
    public class LogDeleteInput
    {
        /// <summary>待删除的日志文件名列表</summary>
        public List<string>? Names { get; set; }
    }
}
