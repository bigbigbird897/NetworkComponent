using System;

namespace ConnectionSocket.LocalEntity
{
    /// <summary>
    /// Socket 服务端最近收到的一条消息（用于 HTTP 接口观测/调试）。
    /// </summary>
    public class ReceivedSocketMessage
    {
        /// <summary>所属服务端编码</summary>
        public string ServerCode { get; set; } = string.Empty;

        /// <summary>来源客户端标识（远端 IP:端口）</summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>原始字节</summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>按服务端编码解码后的字符串</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>接收时间</summary>
        public DateTime Time { get; set; } = DateTime.Now;
    }
}
