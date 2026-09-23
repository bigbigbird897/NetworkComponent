using ArchitectureConfiguration;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Common.License;
using ConnectionMqtt;
using ConnectionSocket;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using NetworkComponent.ArchitectureConfiguration;
using NetworkComponent.License;
using Serilog;
using SqlSugar;

namespace NetworkComponent
{
    /// <summary>
    /// 程序入口。
    /// 统一完成：Serilog 日志、Autofac 依赖注入、Swagger 文档、SqlSugar ORM 的接入，
    /// 以及 MQTT 多客户端的启动初始化。
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // 注册 .NET CodePages 编码提供程序，否则 GBK/GB2312 等中文编码无法使用
            // （Socket 与部分国产设备通信常需 GBK）
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var builder = WebApplication.CreateBuilder(args);

            #region Serilog 日志接入（控制台 + 按天滚动文件）
            builder.Host.UseSerilog((context, services, loggerConfig) => loggerConfig
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(
                    path: Path.Combine("logs", "nc-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));
            #endregion

            #region Autofac 依赖注入容器
            builder
                .Host.UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureContainer<ContainerBuilder>(containerBuilder =>
                {
                    // 批量注册各通信模块（接口 -> 实现，单例）
                    containerBuilder.RegisterModule<AutofacConfig>();
                    // 注册控制器并开启属性注入
                    containerBuilder.RegisterModule<AutofacPropertityRegConfig>();
                });
            // 让控制器也走 Autofac 激活，以便应用其属性注入
            builder.Services.Replace(
                ServiceDescriptor.Transient<IControllerActivator, ServiceBasedControllerActivator>());
            #endregion

            // MVC 控制器
            builder.Services.AddControllers();

            #region Swagger / OpenAPI 文档
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "工业通信中间件 API",
                    Version = "v1",
                    Description = "提供 ModbusTcp / ModbusRtu / MQTT / OPC UA / Socket 等工业设备通信能力"
                });
                // 生成 XML 注释（若项目开启了文档文件输出，此处可注入注释文件路径）
            });
            #endregion

            #region SqlSugar ORM 接入（单例 SqlSugarScope，线程安全）
            builder.Services.AddSingleton<ISqlSugarClient>(sp =>
            {
                var dbType = Enum.Parse<DbType>(
                    builder.Configuration["SqlSugar:DbType"] ?? "SqlServer", ignoreCase: true);
                var connectionString = builder.Configuration["SqlSugar:ConnectionString"] ?? string.Empty;

                var sqlSugar = new SqlSugarScope(new ConnectionConfig
                {
                    ConnectionString = connectionString,
                    DbType = dbType,
                    // 每次操作自动关闭连接，避免连接泄漏
                    IsAutoCloseConnection = true,
                    ConfigureExternalServices = new ConfigureExternalServices()
                });

                // 简易 SQL 执行日志（开发期可在此打印 sqlSugar.Aop）
                sqlSugar.Aop.OnLogExecuting = (sql, parameters) =>
                {
                    Log.Debug("[SqlSugar] {Sql}", sql);
                };
                return sqlSugar;
            });
            #endregion

            #region 授权与接口访问控制（机器绑定 license + API Key + IP 白名单）
            var licenseSettings = builder.Configuration.GetSection("License").Get<LicenseSettings>() ?? new LicenseSettings();
            builder.Services.AddSingleton(licenseSettings);
            builder.Services.AddSingleton(new LicenseManager(licenseSettings));
            #endregion

            var app = builder.Build();

            #region HTTP 请求管道
            // 开发环境启用 Swagger UI
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NetworkComponent v1");
                    // 根路径直接打开 Swagger UI
                    c.RoutePrefix = "swagger";
                });
            }

            // 记录每个 HTTP 请求的 Serilog 日志
            app.UseSerilogRequestLogging();

            // 托管前端控制台（dist 产物放到 wwwroot，根路径即打开 SPA）
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.MapFallbackToFile("index.html");

            // 授权 + API Key + IP 白名单 网关（放在业务路由之前）
            app.UseMiddleware<LicenseGuardMiddleware>();

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();
            #endregion

            #region 启动时初始化 MQTT 多客户端（失败不阻断主程序）
            try
            {
                var mqttService = app.Services.GetService<IMqttClientService>();
                if (mqttService != null)
                {
                    await mqttService.InitAllClientsAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "MQTT 客户端启动初始化失败，将在首次调用时按需重连");
            }
            #endregion

            #region 启动时初始化 Socket 多服务端（失败不阻断主程序）
            try
            {
                var socketServerService = app.Services.GetService<ISocketServerService>();
                if (socketServerService != null)
                {
                    await socketServerService.InitAllServersAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "MQTT 客户端启动初始化失败，将在首次调用时按需重连");
            }
            #endregion

            await app.RunAsync();
        }
    }
}
