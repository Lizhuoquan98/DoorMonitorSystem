using Dapper;
using MySql.Data.MySqlClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace DoorMonitorSystem.Assets.Helper
{
    public class DatabaseHelper
    {
        private string _connectionString;

        public DatabaseHelper(string connectionString)
        {
            _connectionString = connectionString;
        }

        // 通用的数据库读取方法
        public List<T> GetDataFromDatabase<T>(string query, Func<MySqlDataReader, T> map)
        {
            List<T> resultList = [];

            try
            {
                using MySqlConnection conn = new MySqlConnection(_connectionString);
                conn.Open();
                Console.WriteLine("数据库连接成功！");

                using MySqlCommand cmd = new MySqlCommand(query, conn);
                using MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    resultList.Add(map(reader));  // 使用提供的映射函数转换每一行数据
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("数据库连接或查询出错: " + ex.Message);
            }

            return resultList;
        }



        public void EnsureTablesExist()
        {

            try
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();
                Debug.WriteLine("数据库连接成功！");

                // 创建 AlarmModeDict 表
                conn.Execute("""
                             CREATE TABLE IF NOT EXISTS AlarmModeDict (
                                 Id INT AUTO_INCREMENT PRIMARY KEY,
                                 Name VARCHAR(50) NOT NULL UNIQUE
                             );
                         """);

                // 创建 RecordLevelDict 表
                conn.Execute("""
                                 CREATE TABLE IF NOT EXISTS RecordLevelDict (
                                 Id INT AUTO_INCREMENT PRIMARY KEY,
                                 Name VARCHAR(50) NOT NULL UNIQUE
                             );
                         """);

                // 创建 BitDescriptionDict 表
                conn.Execute("""
                             CREATE TABLE IF NOT EXISTS BitDescriptionDict (
                                 Id INT AUTO_INCREMENT PRIMARY KEY,
                                 BitStatusDesc VARCHAR(50) NOT NULL 
                                 );
                           """);

                // 创建 device_point 表
                string createTableSql = """
                              CREATE TABLE IF NOT EXISTS devicePoint (
                                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY COMMENT '主键 ID',
                                  devid INT NOT NULL COMMENT '设备ID',
                                  point_id INT NOT NULL COMMENT '点位编号',
                                  db_number INT NOT NULL COMMENT 'DB 编号',
                                  description VARCHAR(255) DEFAULT '' COMMENT '点位描述',
                                  var_type INT NOT NULL COMMENT '数据类型（枚举索引）',
                                  address INT NOT NULL COMMENT '寄存器地址',
                                  bit_offset INT NOT NULL COMMENT '位地址',
                                  is_alarm INT NOT NULL COMMENT '报警模式（枚举索引）',
                                  record_level INT NOT NULL COMMENT '记录等级（枚举索引）',
                                  bit_high VARCHAR(50) DEFAULT '触发' COMMENT '高位描述',
                                  bit_low VARCHAR(50) DEFAULT '取消' COMMENT '低位描述',
                                  modbus_address VARCHAR(100) DEFAULT '' COMMENT '关联 Modbus 地址',
                                  ui_binding VARCHAR(255) DEFAULT '' COMMENT '关联 UI 路径'
                              ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='S7 通讯点位配置表';
                           """;
                conn.Execute(createTableSql);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("数据库连接或表创建出错: " + ex.Message); 
            }


        }


    }
}
