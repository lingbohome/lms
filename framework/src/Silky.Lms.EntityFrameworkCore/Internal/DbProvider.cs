using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Silky.Lms.Core;
using Silky.Lms.Core.Extensions;
using Silky.Lms.EntityFrameworkCore.Contexts.Attributes;
using Silky.Lms.EntityFrameworkCore.Interceptors;

namespace Silky.Lms.EntityFrameworkCore.Internal
{
    internal static class DbProvider
    {
        /// <summary>
        /// SqlServer 提供器程序集
        /// </summary>
        public const string SqlServer = "Microsoft.EntityFrameworkCore.SqlServer";

        /// <summary>
        /// Sqlite 提供器程序集
        /// </summary>
        public const string Sqlite = "Microsoft.EntityFrameworkCore.Sqlite";

        /// <summary>
        /// Cosmos 提供器程序集
        /// </summary>
        public const string Cosmos = "Microsoft.EntityFrameworkCore.Cosmos";

        /// <summary>
        /// 内存数据库 提供器程序集
        /// </summary>
        public const string InMemoryDatabase = "Microsoft.EntityFrameworkCore.InMemory";

        /// <summary>
        /// MySql 提供器程序集
        /// </summary>
        public const string MySql = "Pomelo.EntityFrameworkCore.MySql";

        /// <summary>
        /// MySql 官方包（更新不及时，只支持 8.0.23+ 版本， 所以单独弄一个分类）
        /// </summary>
        public const string MySqlOfficial = "MySql.EntityFrameworkCore";

        /// <summary>
        /// PostgreSQL 提供器程序集
        /// </summary>
        public const string Npgsql = "Npgsql.EntityFrameworkCore.PostgreSQL";

        /// <summary>
        /// Oracle 提供器程序集
        /// </summary>
        public const string Oracle = "Oracle.EntityFrameworkCore";

        /// <summary>
        /// Firebird 提供器程序集
        /// </summary>
        public const string Firebird = "FirebirdSql.EntityFrameworkCore.Firebird";

        /// <summary>
        /// Dm 提供器程序集
        /// </summary>
        public const string Dm = "Microsoft.EntityFrameworkCore.Dm";
        
        /// <summary>
        /// 获取数据库上下文连接字符串
        /// </summary>
        /// <typeparam name="TDbContext"></typeparam>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public static string GetConnectionString<TDbContext>(string connectionString = default)
            where TDbContext : DbContext
        {
            // 支持读取配置渲染
            var realConnectionString = connectionString.Render();
            
            if (!string.IsNullOrWhiteSpace(realConnectionString)) return realConnectionString;
            
            // 如果没有配置数据库连接字符串，那么查找特性
            var dbContextAttribute = GetAppDbContextAttribute(typeof(TDbContext));
            if (dbContextAttribute == null) return default;
            
            // 获取特性连接字符串（渲染配置模板）
            var connStr = dbContextAttribute.ConnectionString.Render();
            
            if (string.IsNullOrWhiteSpace(connStr)) return default;
            // 如果包含 = 符号，那么认为是连接字符串
            if (connStr.Contains("=")) return connStr;
            else
            {
                var configuration = EngineContext.Current.Configuration;
            
                // 如果包含 : 符号，那么认为是一个 Key 路径
                if (connStr.Contains(":")) return configuration[connStr];
                else
                {
                    // 首先查找 DbConnectionString 键，如果没有找到，则当成 Key 去查找
                    var connStrValue = configuration.GetConnectionString(connStr);
                    return !string.IsNullOrWhiteSpace(connStrValue) ? connStrValue : configuration[connStr];
                }
            }
        }

        /// <summary>
        /// 数据库上下文 [AppDbContext] 特性缓存
        /// </summary>
        private static readonly ConcurrentDictionary<Type, AppDbContextAttribute> DbContextAppDbContextAttributes;
        
        internal static AppDbContextAttribute GetAppDbContextAttribute(Type dbContexType)
        {
            return DbContextAppDbContextAttributes.GetOrAdd(dbContexType, Function);

            // 本地静态函数
            static AppDbContextAttribute Function(Type dbContextType)
            {
                if (!dbContextType.IsDefined(typeof(AppDbContextAttribute), true)) return default;

                var appDbContextAttribute = dbContextType.GetCustomAttribute<AppDbContextAttribute>(true);

                return appDbContextAttribute;
            }
        }

        internal static bool IsDatabaseFor(string providerName, string dbAssemblyName)
        {
            return providerName.Equals(dbAssemblyName, StringComparison.Ordinal);
        }

        public static List<IInterceptor> GetDefaultInterceptors()
        {
            return new() 
            {
               new SqlConnectionProfilerInterceptor(),
               new SqlCommandProfilerInterceptor(),
               new DbContextSaveChangesInterceptor()
            };
        }
    }
}