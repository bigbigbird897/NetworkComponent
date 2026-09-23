using Autofac;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace ArchitectureConfiguration
{
    /// <summary>
    /// Autofac 模块注册类：实现控制器属性自动注入。
    /// 继承 Autofac 的 Module，程序启动时由主项目通过 RegisterModule 加载。
    /// </summary>
    public class AutofacPropertityRegConfig : Autofac.Module
    {
        /// <summary>
        /// 重写 Autofac 模块加载方法，所有依赖注册逻辑写在此处。
        /// </summary>
        /// <param name="builder">Autofac 容器构建器，用于批量注册服务、控制器</param>
        protected override void Load(ContainerBuilder builder)
        {
            // 获取控制器基类类型（所有 Api 控制器都继承 ControllerBase）
            var controllerBaseType = typeof(ControllerBase);

            // 入口程序集即主 Web 项目（Program 所在程序集）。
            // 注意：本类库不能直接引用主项目（会形成循环引用），
            // 因此通过入口程序集定位控制器所在程序集。
            var entryAssembly = Assembly.GetEntryAssembly()
                                  ?? typeof(AutofacPropertityRegConfig).Assembly;

            // 1. 扫描程序入口所在程序集
            builder
                .RegisterAssemblyTypes(entryAssembly)
                // 2. 过滤类型：只筛选继承 ControllerBase、且不等于基类本身的类型（所有自定义 Api 控制器）
                .Where(t => controllerBaseType.IsAssignableFrom(t) && t != controllerBaseType)
                // 3. 开启属性自动注入：控制器中带有 public 的服务属性，自动完成依赖注入，无需构造函数传参
                .PropertiesAutowired();
        }
    }
}
