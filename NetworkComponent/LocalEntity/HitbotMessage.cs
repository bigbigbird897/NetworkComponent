using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NetworkComponent.LocalEntity
{
    /// <summary>
    /// Hitbot 统一交互报文模型
    /// message_type: request / response
    /// 已合并：移除 HitbotMessModel<T> 子类，全部能力内聚到本类；通过泛型方法/静态工厂实现强类型参数
    /// </summary>
    public class HitbotMessage
    {
        /// <summary>消息类型：request / response</summary>
        public string message_type { get; set; } = string.Empty;

        /// <summary>唯一消息ID，GUID无横杠字符串</summary>
        public string message_id { get; set; } = string.Empty;

        /// <summary>指令名称</summary>
        public string instruct { get; set; } = string.Empty;

        /// <summary>业务动态参数集合</summary>
        public JObject set { get; set; } = new JObject();

        /// <summary>设备运行状态</summary>
        public StatusModel status { get; set; } = new StatusModel();

        /// <summary>无参构造</summary>
        public HitbotMessage(){}

        /// <summary>构造请求报文，传入指令名与JObject动态参数</summary>
        /// <param name="instructName">指令名称</param>
        /// <param name="setPara">动态参数JObject</param>
        public HitbotMessage(string instructName, JObject? setPara)
        {
            message_type = "request";
            message_id = Guid.NewGuid().ToString("N");
            instruct = instructName;
            set = setPara ?? new JObject();
            status = new StatusModel();
        }

        #region 静态工厂（替代原来 HitbotMessModel<T> 的构造器）

        /// <summary>
        /// 【工厂】创建请求报文，传入强类型参数实体（替代 new HitbotMessModel<T>(instruct,entity)）
        /// </summary>
        /// <typeparam name="T">参数实体类型</typeparam>
        /// <param name="instructName">指令名称</param>
        /// <param name="paramEntity">强类型参数实体</param>
        /// <returns></returns>
        public static HitbotMessage CreateRequest<T>(string instructName, T? paramEntity)
        {
            var jObj = paramEntity == null ? new JObject() : JObject.FromObject(paramEntity);
            var msg = new HitbotMessage(instructName, jObj);
            return msg;
        }

        /// <summary>
        /// 【工厂】创建请求报文，无set参数 set={}
        /// </summary>
        public static HitbotMessage CreateRequest(string instructName)
        {
            return new HitbotMessage(instructName, new JObject());
        }

        /// <summary>生成响应报文（复用message_id）</summary>
        /// <param name="requestMessage">原始请求报文</param>
        public static HitbotMessage CreateResponse(HitbotMessage requestMessage)
        {
            return new HitbotMessage
            {
                message_type = "response",
                message_id = requestMessage.message_id,
                instruct = requestMessage.instruct,
                set = new JObject(),
                status = new StatusModel()
            };
        }

        #endregion

        #region JSON序列化反序列化

        /// <summary>序列化为JSON字符串</summary>
        /// <param name="indented">是否格式化缩进，MQTT传输默认false紧凑</param>
        /// <returns>JSON报文</returns>
        public string ToJsonString(bool indented = false)
        {
            var fmt = indented ? Formatting.Indented : Formatting.None;
            return JsonConvert.SerializeObject(this, fmt);
        }

        /// <summary>静态：JSON字符串反序列化为HitbotMess</summary>
        public static HitbotMessage? FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            return JsonConvert.DeserializeObject<HitbotMessage>(json);
        }

        #endregion

        /// <summary>把set字段解析为指定强类型实体，空防护</summary>
        /// <typeparam name="T">目标实体类型</typeparam>
        /// <returns></returns>
        public T GetSet<T>()
        {
            if (set == null)
                return Activator.CreateInstance<T>();
            // ToObject 可能返回 null，兜底创建默认实例
            return set.ToObject<T>() ?? Activator.CreateInstance<T>();
        }
    }

    /// <summary>设备状态子实体</summary>
    public class StatusModel
    {
        /// <summary>工作状态 1运行 0停止</summary>
        public int work { get; set; } = 1;

        /// <summary>报警信息，无报警为空字符串</summary>
        public string alarm_mes { get; set; } = string.Empty;
    }
}
