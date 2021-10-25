﻿using System;
using Silky.EntityFrameworkCore.Contexts.Enums;

namespace Silky.EntityFrameworkCore.Contexts.Attributes
{
    public class AppDbContextAttribute : Attribute
    {
        /// <summary>
        /// 默认构造函数
        /// </summary>
        /// <param name="slaveDbContextLocators"></param>
        public AppDbContextAttribute(params Type[] slaveDbContextLocators)
        {
            SlaveDbContextLocators = slaveDbContextLocators;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="slaveDbContextLocators"></param>
        public AppDbContextAttribute(string connectionString, params Type[] slaveDbContextLocators)
        {
            ConnectionString = connectionString;
            SlaveDbContextLocators = slaveDbContextLocators;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="providerName"></param>
        /// <param name="slaveDbContextLocators"></param>
        public AppDbContextAttribute(string connectionString, string providerName, params Type[] slaveDbContextLocators)
        {
            ConnectionString = connectionString;
            ProviderName = providerName;
            SlaveDbContextLocators = slaveDbContextLocators;
        }

        /// <summary>
        /// 数据库连接字符串
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// 数据库提供器名称
        /// </summary>
        public string ProviderName { get; set; }

        /// <summary>
        /// 数据库上下文模式
        /// </summary>
        public DbContextMode Mode { get; set; } = DbContextMode.Cached;

        /// <summary>
        /// 表统一前缀
        /// </summary>
        public string TablePrefix { get; set; }

        /// <summary>
        /// 表统一后缀
        /// </summary>
        public string TableSuffix { get; set; }

        /// <summary>
        /// 指定从库定位器
        /// </summary>
        public Type[] SlaveDbContextLocators { get; set; }
    }
}