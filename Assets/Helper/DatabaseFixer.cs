using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using DoorMonitorSystem.Models.system;
using MySql.Data.MySqlClient;

namespace DoorMonitorSystem.Assets.Helper
{
    public static class DatabaseFixer
    {
        public static void FixSchema(string server, string user, string pwd, string dbName)
        {
            try
            {
                using var db = new SQLHelper(server, user, pwd, dbName);
                db.Connect();

                // 需要修复的表列表
                VerifyAndFixTable<SysDeviceEntity>(db, "SysDeviceEntity");
                VerifyAndFixTable<SysDeviceParamEntity>(db, "SysDeviceParamEntity");
                VerifyAndFixTable<SysGraphicGroupEntity>(db, "SysGraphicGroupEntity");
                VerifyAndFixTable<SysGraphicItemEntity>(db, "SysGraphicItemEntity");
                VerifyAndFixTable<SysSettingsEntity>(db, "SysSettingsEntity");

                // 修正遗留数据的 LogTypeId=0 问题 (默认为1)
                db.ExecuteNonQuery("UPDATE DevicePointConfig SET LogTypeId = 1 WHERE LogTypeId = 0");
            }
            catch (Exception ex)
            {
                LogHelper.Error("[DatabaseFixer] 数据库修复流程异常", ex);
            }
        }

        private static void VerifyAndFixTable<T>(SQLHelper db, string tableName) where T : new()
        {
            try
            {
                // 1. 检查是否存在主键
                if (HasPrimaryKey(db, tableName))
                {
                    // Debug.WriteLine($"[DatabaseFixer] 表 {tableName} 已有主键，无需修复。");
                    return;
                }

                LogHelper.Info($"[DatabaseFixer] 检测到表 {tableName} 缺少主键，开始修复...");

                // 2. 重命名旧表
                string backupName = $"{tableName}_Old_{DateTime.Now:yyyyMMddHHmmss}";
                db.ExecuteNonQuery($"RENAME TABLE `{tableName}` TO `{backupName}`");
                LogHelper.Info($"[DatabaseFixer] 已备份旧表为: {backupName}");

                // 3. 创建新表 (使用最新的带 Key 属性的实体)
                db.CreateTableFromModel<T>(tableName);
                LogHelper.Info($"[DatabaseFixer] 已创建新表结构: {tableName}");

                // 4. 迁移数据
                // 获取新旧表的公共字段 (排除 Id，让其自增)
                var columns = GetCommonColumns(db, backupName, tableName);
                columns.Remove("Id"); // 排除 Id，让新表自动生成唯一 ID

                if (columns.Count > 0)
                {
                    string colStr = string.Join(", ", columns);
                    string sql = $"INSERT INTO `{tableName}` ({colStr}) SELECT {colStr} FROM `{backupName}`";
                    int count = db.ExecuteNonQuery(sql);
                    LogHelper.Info($"[DatabaseFixer] 数据迁移完成，共 {count} 行。");
                }

                // 5. 删除旧表
                db.DropTableIfExists(backupName);
                LogHelper.Info($"[DatabaseFixer] 已清理备份表: {backupName}");

            }
            catch (Exception ex)
            {
                LogHelper.Error($"[DatabaseFixer] 修复表 {tableName} 失败", ex);
            }
        }

        private static bool HasPrimaryKey(SQLHelper db, string tableName)
        {
            string sql = @"
                SELECT COUNT(*) 
                FROM information_schema.table_constraints 
                WHERE table_schema = @db 
                AND table_name = @tb 
                AND constraint_type = 'PRIMARY KEY'";
            
            var result = db.ExecuteScalar(sql, 
                new MySqlParameter("@db", db.DatabaseName),
                new MySqlParameter("@tb", tableName));
            
            return Convert.ToInt32(result) > 0;
        }

        private static List<string> GetCommonColumns(SQLHelper db, string table1, string table2)
        {
            try 
            {
                var cols1 = GetColumnNames(db, table1);
                var cols2 = GetColumnNames(db, table2);
                
                // 取交集
                var common = new List<string>();
                foreach (var c in cols1)
                {
                    if (cols2.Contains(c)) common.Add($"`{c}`");
                }
                return common;
            }
            catch
            {
                return new List<string>();
            }
        }

        private static List<string> GetColumnNames(SQLHelper db, string tableName)
        {
            var list = new List<string>();
            var dt = db.GetTableColumns(tableName);
            foreach (System.Data.DataRow row in dt.Rows)
            {
                list.Add(row["列名"].ToString());
            }
            return list;
        }
    }
}
