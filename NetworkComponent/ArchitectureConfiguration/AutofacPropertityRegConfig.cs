using Autofac;
using Microsoft.AspNetCore.Mvc;

namespace NetworkComponent.ArchitectureConfiguration
{
    /// <summary>
    /// Autofac模块注册类：实现控制器属性自动注入
    /// 继承Autofac的Module，程序启动时自动加载该模块配置
    /// </summary>
    public class AutofacPropertityRegConfig : Module
    {
        /// <summary>
        /// 重写Autofac模块加载方法，所有依赖注册逻辑写在此处
        /// </summary>
        /// <param name="builder">Autofac容器构建器，用于批量注册服务、控制器</param>
        protected override void Load(ContainerBuilder builder)
        {
            // 获取控制器基类类型（所有Api控制器都继承ControllerBase）
            var controllerBaseType = typeof(ControllerBase);

            // 1.扫描程序入口所在程序集（Program类所在程序集）
            builder
                .RegisterAssemblyTypes(typeof(Program).Assembly)
                // 2.过滤类型：只筛选继承ControllerBase、且不等于基类本身的类型（所有自定义Api控制器）
                .Where(t => controllerBaseType.IsAssignableFrom(t) && t != controllerBaseType)
                // 3.开启属性自动注入：控制器中带有public的服务属性，自动完成依赖注入，无需构造函数传参
                .PropertiesAutowired();
        }
    }
}
