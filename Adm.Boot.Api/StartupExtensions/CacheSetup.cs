﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdmBoots.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AdmBoots.Api.StartupExtensions {
    public static class CacheSetup {
        /// <summary>
        /// 分布式缓存 注入接口IDistributedCache
        /// </summary>
        /// <param name="services"></param>
        public static void AddCacheSetup(this IServiceCollection services) {

            if (services == null) throw new ArgumentNullException(nameof(services));

            var connection = AdmBootsApp.Configuration["Redis:Configuration"];
            var instanceName = AdmBootsApp.Configuration["Redis:InstanceName"];

            if (!string.IsNullOrEmpty(connection)) {
                var redis = ConnectionMultiplexer.Connect(connection);//建立Redis 连接

                //添加数据保护服务，设置统一应用程序名称，并指定使用Reids存储私钥
                services.AddDataProtection()
                    .SetApplicationName("AdmBoots")
                    .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys");

                //添加Redis缓存用于分布式Session
                services.AddStackExchangeRedisCache(options => {
                    options.Configuration = connection;
                    options.InstanceName = instanceName;
                });
            } else {
                services.AddDistributedMemoryCache();
            }
        }
    }
}
