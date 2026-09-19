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
        /// 重写 Autofac 模块加载方法，所有批量注册逻辑写在此处。
        /// </summary>
        /// <param name="builder">Autofac 容器构建器</param>
        protected override void Load(ContainerBuilder builder)
        {
            // 程序运行根目录（与 Web 项目输出目录一致）
            var basePath = AppContext.BaseDirectory;

            foreach (var dllFile in ConnectionAssemblyFiles)
            {
                var fullPath = Path.Combine(basePath, dllFile);
                // 文件不存在时跳过，避免未被主项目引用的模块导致启动异常
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                var assembly = Assembly.LoadFrom(fullPath);
                builder.RegisterAssemblyTypes(assembly)
                       .AsImplementedInterfaces()   // 按实现的接口注册，如 IModbusTcpClient -> ModbusTcpClient
                       .SingleInstance()            // 通信客户端需长驻，单例复用连接与缓存
                       .PropertiesAutowired();      // 支持属性自动注入
            }
        }
    }
}
