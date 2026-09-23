using Autofac;
using System.Reflection;

namespace ArchitectureConfiguration
{
    /// <summary>
    /// Autofac 批量注册模块。
    /// 统一扫描各通信模块程序集，将其中“接口 -> 实现”以单例方式注册到容器，并开启属性自动注入。
    /// 备注：
    /// 1. ContainerBuilder：容器构建器，用来注册所有服务；
    /// 2. IContainer：构建完成的根容器；
    /// 3. ILifetimeScope：作用域，日常业务优先使用，不要直接用根容器 Resolve。
    /// 只有后台任务、动态工厂等无 HttpContext 的场景才需要手动拿容器/Scope。
    /// </summary>
    public class AutofacConfig : Autofac.Module
    {
        /// <summary>
        /// 需要批量注册的通信模块程序集文件名（位于程序运行根目录）。
        /// 新增通信模块时，在此数组中追加对应 dll 名即可自动接入 DI。
        /// </summary>
        private static readonly string[] ConnectionAssemblyFiles = new[]
        {
            "ConnectionModbusRtuWithTcp.dll",
            "ConnectionModbusTcp.dll",
            "ConnectionMqtt.dll",
            "ConnectionOPCUA.dll",
            "ConnectionSocket.dll"
        };

        /// <summary>
        /// 需要批量注册的增强服务程序集文件名（Service 层：配置/授权/日志/记事本等业务服务）。
        /// 同样按“接口 -> 实现”以单例方式注册，供控制器属性注入使用。
        /// </summary>
        private static readonly string[] EnhanceServiceAssemblyFiles = new[]
        {
            "ServiceComponentEnhance.dll"
        };

        /// <summary>
        /// 重写 Autofac 模块加载方法，所有批量注册逻辑写在此处。
        /// </summary>
        /// <param name="builder">Autofac 容器构建器</param>
        protected override void Load(ContainerBuilder builder)
        {
            // 程序运行根目录（与 Web 项目输出目录一致）
            var basePath = AppContext.BaseDirectory;

            // 注册各通信模块：接口 -> 实现，单例，属性注入
            foreach (var dllFile in ConnectionAssemblyFiles)
            {
                RegisterAssemblyTypes(builder, basePath, dllFile);
            }

            // 注册增强服务（Service 层）：接口 -> 实现，单例，属性注入
            foreach (var dllFile in EnhanceServiceAssemblyFiles)
            {
                RegisterAssemblyTypes(builder, basePath, dllFile);
            }
        }

        /// <summary>
        /// 加载指定程序集并把其中“接口 -> 实现”批量注册到容器（单例 + 属性注入）。
        /// </summary>
        /// <param name="builder">Autofac 容器构建器</param>
        /// <param name="basePath">程序运行根目录</param>
        /// <param name="dllFile">待注册的程序集文件名（含 .dll）</param>
        private static void RegisterAssemblyTypes(ContainerBuilder builder, string basePath, string dllFile)
        {
            var fullPath = Path.Combine(basePath, dllFile);
            // 文件不存在时跳过，避免未被主项目引用的模块导致启动异常
            if (!File.Exists(fullPath))
            {
                return;
            }

            var assembly = Assembly.LoadFrom(fullPath);
            builder.RegisterAssemblyTypes(assembly)
                   .AsImplementedInterfaces()   // 按实现的接口注册，如 IModbusTcpClient -> ModbusTcpClient
                   .SingleInstance()            // 通信客户端/业务服务需长驻，单例复用连接与缓存
                   .PropertiesAutowired();      // 支持属性自动注入
        }
    }
}
