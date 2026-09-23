namespace ServiceComponentEnhance.LocalEntity
{
    /// <summary>记事本保存/删除入参</summary>
    public class NoteInput
    {
        /// <summary>文件名（仅简单名称，禁止路径分隔符与 ..）</summary>
        public string? Name { get; set; }

        /// <summary>文件全文（保存时使用）</summary>
        public string? Content { get; set; }
    }
}
