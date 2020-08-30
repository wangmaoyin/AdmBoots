﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AdmBoots.Application;
using AdmBoots.Data.EntityFrameworkCore;
using AdmBoots.Domain.IRepositories;
using AdmBoots.Infrastructure;
using Autofac;
using Autofac.Extras.DynamicProxy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AdmBoots.Api.StartupExtensions;
using Newtonsoft.Json;
using AdmBoots.Api.Filters;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using AdmBoots.Infrastructure.Config;
using AdmBoots.Data.EntityFrameworkCore.Uow;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;

namespace AdmBoots.Api {

    public class Startup {

        public static readonly ILoggerFactory EFLoggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });

        public Startup(IWebHostEnvironment env) {
            AdmBootsApp.Configuration = new ConfigurationBuilder()
             .SetBasePath(env.ContentRootPath)
            //optional: true配置文件不存在时抛异常 ReloadOnChange= true 热更新
            //.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .Add(new JsonConfigurationSource { Path = "appsettings.json", ReloadOnChange = true })
            .Build();
        }

        /// <summary>
        /// AutoFac容器
        /// </summary>
        /// <param name="builder"></param>
        public void ConfigureContainer(ContainerBuilder builder) {
            //注册拦截器
            builder.RegisterType<UnitOfWorkInterceptor>().AsSelf();
            builder.RegisterType<HttpContextAccessor>().As<IHttpContextAccessor>().SingleInstance();
            //builder.RegisterType<AdminSession>().As<IAdminSession>();

            #region Application层注入
            //AdmBoots.Application是继承接口的实现方法类库名称
            var assemblys = Assembly.Load("AdmBoots.Application");
            //ITransientDependency 是一个接口（所有Application层要实现依赖注入的接口都要继承该接口）
            var baseType = typeof(ITransientDependency);
            builder.RegisterAssemblyTypes(assemblys)
                .Where(m => baseType.IsAssignableFrom(m) && m != baseType && !m.IsAbstract)
            .AsImplementedInterfaces()
            .PropertiesAutowired()                       //支持属性注入
            .EnableInterfaceInterceptors()               //启用接口拦截
            .InterceptedBy(typeof(UnitOfWorkInterceptor));
            #endregion

            #region Data层注入
            //Data层实现接口得类自动依赖注入
            var basePath = AppContext.BaseDirectory;
            var repositoryDllFile = Path.Combine(basePath, "AdmBoots.Data.dll");
            var assemblysRepository = Assembly.LoadFrom(repositoryDllFile);
            builder.RegisterAssemblyTypes(assemblysRepository)
                .AsImplementedInterfaces();
            #endregion

            builder.RegisterGeneric(typeof(AdmRepositoryBase<,>)).As(typeof(IRepository<,>)).InstancePerDependency();

        }

        public void ConfigureServices(IServiceCollection services) {
            services.AddSwaggerSetup();
            services.AddCacheSetup();
            services.AddAuthorizationSetup();
            services.AddHealthChecksSetup();
            services.AddAutoMapper(Assembly.Load("AdmBoots.Application"));
            services.AddApiVersioning(option => option.ReportApiVersions = true);
            services.AddDbContext<AdmDbContext>(option => option
                .UseMySql(DatabaseConfig.ConnectionString)
                .UseLoggerFactory(EFLoggerFactory));
            services.AddControllers(option => {
                option.Filters.Add(typeof(GlobalExceptionFilter));
            }).AddNewtonsoftJson(option => {
                //忽略循环引用
                option.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IApiVersionDescriptionProvider provider) {
            AdmBootsApp.ServiceProvider = app.ApplicationServices;

            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            }
            //↓↓↓↓注意以下中间件顺序↓↓↓↓

            app.UseStaticFiles();

            app.UseRouting();
            //认证
            app.UseAuthentication();
            //授权
            app.UseAuthorization();

            app.UseEndpoints(endpoints => {
                endpoints.MapControllers();
                //健康检查 输出描述信息
                endpoints.MapHealthChecks("/healthz", new HealthCheckOptions {
                    Predicate = _ => true,
                    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                });
                endpoints.MapHealthChecksUI();
            });

            app.UseSwagger();
            app.UseSwaggerUI(c => {
                foreach (var description in provider.ApiVersionDescriptions) {
                    c.SwaggerEndpoint(
                        $"/swagger/{description.GroupName}/swagger.json",
                        description.GroupName.ToUpperInvariant());
                }
                c.IndexStream = () => Assembly.GetExecutingAssembly()
                   .GetManifestResourceStream("AdmBoots.Api.wwwroot.swagger.index.html");
                c.RoutePrefix = "";//设置为空，launchSettings.json把launchUrl去掉,localhost:8082 代替 localhost:8002/swagger
            });





        }
    }
}
