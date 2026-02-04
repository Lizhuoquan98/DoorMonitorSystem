using System;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Models.ConfigEntity.Door;
using DoorMonitorSystem.Models.ConfigEntity.Group;

namespace DoorMonitorSystem.Assets.Helper
{
    public static class BusinessDatabaseFixer
    {
        public static void FixSchema(string serverAddress, string userName, string userPassword, string databaseName)
        {
            try
            {
                using var db = new SQLHelper(serverAddress, userName, userPassword, databaseName);
                db.Connect();

                if (!db.IsConnected) return;

                // 1. 确保所有基础表存在
                db.CreateTableFromModel<StationEntity>();
                db.CreateTableFromModel<StationTypeEntity>();
                db.CreateTableFromModel<DoorGroupEntity>();
                db.CreateTableFromModel<PanelGroupEntity>();
                db.CreateTableFromModel<DoorEntity>();
                db.CreateTableFromModel<PanelEntity>();
                db.CreateTableFromModel<DoorTypeEntity>();
                db.CreateTableFromModel<PanelTypeEntity>();
                db.CreateTableFromModel<BitCategoryEntity>();
                db.CreateTableFromModel<BitColorEntity>();
                db.CreateTableFromModel<DoorBitConfigEntity>();
                db.CreateTableFromModel<PanelBitConfigEntity>();

                // 2. 确保站台参数相关表存在
                db.CreateTableFromModel<AsdModelMappingEntity>();
                db.CreateTableFromModel<ParameterDefineEntity>();

                // 3. 补齐字段 (针对存量数据库)
                EnsureColumnExists(db, "Station", "KeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "DoorGroup", "KeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "Door", "KeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "PanelGroup", "KeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "Panel", "KeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "DoorBitConfig", "KeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "PanelBitConfig", "KeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "DoorType", "KeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "PanelType", "KeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "Sys_ParameterDefines", "KeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "Sys_AsdModels", "KeyId", "VARCHAR(50) DEFAULT NULL");

                // 4. 初始化 GUID 数据
                db.ExecuteNonQuery("UPDATE Station SET KeyId = UUID() WHERE KeyId IS NULL OR KeyId = ''");
                db.ExecuteNonQuery("UPDATE DoorGroup SET KeyId = UUID() WHERE KeyId IS NULL OR KeyId = ''");
                db.ExecuteNonQuery("UPDATE Door SET KeyId = UUID() WHERE KeyId IS NULL OR KeyId = ''");
                db.ExecuteNonQuery("UPDATE PanelGroup SET KeyId = UUID() WHERE KeyId IS NULL OR KeyId = ''");
                db.ExecuteNonQuery("UPDATE Panel SET KeyId = UUID() WHERE KeyId IS NULL OR KeyId = ''");
                db.ExecuteNonQuery("UPDATE DoorBitConfig SET KeyId = UUID() WHERE KeyId IS NULL OR KeyId = ''");
                db.ExecuteNonQuery("UPDATE PanelBitConfig SET KeyId = UUID() WHERE KeyId IS NULL OR KeyId = ''");
                db.ExecuteNonQuery("UPDATE DoorType SET KeyId = UUID() WHERE KeyId IS NULL OR KeyId = ''");
                db.ExecuteNonQuery("UPDATE PanelType SET KeyId = UUID() WHERE KeyId IS NULL OR KeyId = ''");
                db.ExecuteNonQuery("UPDATE Sys_ParameterDefines SET KeyId = UUID() WHERE KeyId IS NULL OR KeyId = ''");
                db.ExecuteNonQuery("UPDATE Sys_AsdModels SET KeyId = UUID() WHERE KeyId IS NULL OR KeyId = ''");

                // 5. 补齐业务字段
                EnsureColumnExists(db, "Sys_ParameterDefines", "ByteOffset", "INT DEFAULT 0");
                EnsureColumnExists(db, "Sys_ParameterDefines", "BitIndex", "INT DEFAULT 0");
                EnsureColumnExists(db, "Sys_ParameterDefines", "BindingKey", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "Sys_ParameterDefines", "PlcPermissionValue", "INT DEFAULT 1");
                EnsureColumnExists(db, "Sys_ParameterDefines", "SortOrder", "INT DEFAULT 0");
                EnsureColumnExists(db, "Sys_ParameterDefines", "DataType", "VARCHAR(20) DEFAULT 'Int16'");
                EnsureColumnExists(db, "Sys_AsdModels", "PlcId", "INT DEFAULT 0");

                // 5. 检查并添加缺失的 KeyId 关联列
                EnsureColumnExists(db, "DoorGroup", "StationKeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "DoorGroup", "IsReverseOrder", "TINYINT(1) NOT NULL DEFAULT 0");
                EnsureColumnExists(db, "PanelGroup", "StationKeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "Door", "ParentKeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "Panel", "ParentKeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "Door", "DoorTypeKeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "Panel", "PanelTypeKeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "DoorBitConfig", "DoorTypeKeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "DoorBitConfig", "DataType", "VARCHAR(20) DEFAULT 'Bool'");
                EnsureColumnExists(db, "DoorBitConfig", "SortOrder", "INT NOT NULL DEFAULT 0");
                EnsureColumnExists(db, "DoorBitConfig", "LogTypeId", "INT NOT NULL DEFAULT 1");

                EnsureColumnExists(db, "PanelBitConfig", "PanelKeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "PanelBitConfig", "DataType", "VARCHAR(20) DEFAULT 'Bool'");
                EnsureColumnExists(db, "PanelBitConfig", "SortOrder", "INT NOT NULL DEFAULT 0");
                EnsureColumnExists(db, "PanelBitConfig", "LogTypeId", "INT NOT NULL DEFAULT 1");

                // DevicePointConfig Extensions
                db.CreateTableFromModel<DevicePointConfigEntity>(); // Creates table if not exists with base model. If exists, model changes might not auto-apply if not using EF migrations. We use Alter Table here.
                EnsureColumnExists(db, "DevicePointConfig", "IsSyncEnabled", "TINYINT(1) NOT NULL DEFAULT 0");
                EnsureColumnExists(db, "DevicePointConfig", "SyncTargetDeviceId", "INT DEFAULT NULL");
                EnsureColumnExists(db, "DevicePointConfig", "SyncTargetAddress", "INT DEFAULT NULL");
                EnsureColumnExists(db, "DevicePointConfig", "SyncTargetBitIndex", "INT DEFAULT NULL");
                EnsureColumnExists(db, "DevicePointConfig", "SyncMode", "INT NOT NULL DEFAULT 0");
                EnsureColumnExists(db, "DevicePointConfig", "IsLogEnabled", "TINYINT(1) NOT NULL DEFAULT 0");
                EnsureColumnExists(db, "DevicePointConfig", "LogTypeId", "INT NOT NULL DEFAULT 1");
                EnsureColumnExists(db, "DevicePointConfig", "LogTriggerState", "INT NOT NULL DEFAULT 2");
                EnsureColumnExists(db, "DevicePointConfig", "LogMessage", "VARCHAR(200) DEFAULT NULL");
                EnsureColumnExists(db, "DevicePointConfig", "LogDeadband", "DOUBLE DEFAULT NULL");
                EnsureColumnExists(db, "DevicePointConfig", "Category", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "DevicePointConfig", "KeyId", "VARCHAR(50) DEFAULT NULL"); 
                EnsureColumnExists(db, "DevicePointConfig", "TargetModelKeyId", "VARCHAR(50) DEFAULT NULL");

                // 4. 数据迁移：根据旧的 ID 关联补全 KeyId 关联
                // Station -> DoorGroup/PanelGroup
                if (ColumnExists(db, "DoorGroup", "StationId"))
                    db.ExecuteNonQuery("UPDATE DoorGroup dg JOIN Station s ON dg.StationId = s.Id SET dg.StationKeyId = s.KeyId WHERE dg.StationKeyId IS NULL OR dg.StationKeyId = ''");
                if (ColumnExists(db, "PanelGroup", "StationId"))
                    db.ExecuteNonQuery("UPDATE PanelGroup pg JOIN Station s ON pg.StationId = s.Id SET pg.StationKeyId = s.KeyId WHERE pg.StationKeyId IS NULL OR pg.StationKeyId = ''");

                // DoorGroup -> Door
                if (ColumnExists(db, "Door", "DoorGroupId"))
                    db.ExecuteNonQuery("UPDATE Door d JOIN DoorGroup dg ON d.DoorGroupId = dg.Id SET d.ParentKeyId = dg.KeyId WHERE d.ParentKeyId IS NULL OR d.ParentKeyId = ''");
                
                // PanelGroup -> Panel
                if (ColumnExists(db, "Panel", "PanelGroupId"))
                    db.ExecuteNonQuery("UPDATE Panel p JOIN PanelGroup pg ON p.PanelGroupId = pg.Id SET p.ParentKeyId = pg.KeyId WHERE p.ParentKeyId IS NULL OR p.ParentKeyId = ''");

                // DoorType -> Door
                if (ColumnExists(db, "Door", "DoorTypeId"))
                    db.ExecuteNonQuery("UPDATE Door d JOIN DoorType dt ON d.DoorTypeId = dt.Id SET d.DoorTypeKeyId = dt.KeyId WHERE d.DoorTypeKeyId IS NULL OR d.DoorTypeKeyId = ''");

                // PanelType -> Panel
                if (ColumnExists(db, "Panel", "PanelTypeId"))
                    db.ExecuteNonQuery("UPDATE Panel p JOIN PanelType pt ON p.PanelTypeId = pt.Id SET p.PanelTypeKeyId = pt.KeyId WHERE p.PanelTypeKeyId IS NULL OR p.PanelTypeKeyId = ''");

                // Type -> BitConfig
                if (ColumnExists(db, "DoorBitConfig", "DoorTypeId"))
                    db.ExecuteNonQuery("UPDATE DoorBitConfig bc JOIN DoorType dt ON bc.DoorTypeId = dt.Id SET bc.DoorTypeKeyId = dt.KeyId WHERE bc.DoorTypeKeyId IS NULL OR bc.DoorTypeKeyId = ''");
                // PanelBitConfig: Previously linked to PanelType, now requires PanelKeyId (Manual update or new config required usually).
                // We'll leave PanelKeyId empty for now if not present, as it's specific per panel instance.
                // However, if we want to migrate old type-based configs to instances, we needs a complex script.
                // For now, just ensure the column exists.

                // 5. 清理旧的 ID 列 (谨慎！如果数据已经迁移，可以删除)
                // 这里我们选择保留一段时间，或者仅在确定安全时删除。
                // 为了让 UI 加载直接走 KeyId 逻辑，我们必须确保 StationDataService 使用正确的列名。
                EnsureColumnExists(db, "DevicePointConfig", "TargetModelKeyId", "VARCHAR(50) DEFAULT NULL");
                EnsureColumnExists(db, "DevicePointConfig", "TargetBitConfigKeyId", "VARCHAR(50) DEFAULT NULL");

                // 6. 添加唯一约束 (Unique Constraints)
                // 确保同一个门组下的门序号唯一，同一个面板组下的面板序号唯一
                // 这样 SortOrder 就可以作为逻辑ID使用
                EnsureUniqueIndex(db, "Door", "UK_Door_Parent_Sort", "ParentKeyId, SortOrder");
                EnsureUniqueIndex(db, "Panel", "UK_Panel_Parent_Sort", "ParentKeyId, SortOrder");
            }
            catch (Exception ex)
            {
                LogHelper.Error("[BusinessDatabaseFixer] 业务数据库修复异常", ex);
            }
        }

        private static void EnsureUniqueIndex(SQLHelper db, string tableName, string indexName, string columns)
        {
            try
            {
                // Check if index exists
                string checkSql = $@"
                    SELECT COUNT(*) 
                    FROM information_schema.statistics 
                    WHERE table_schema = '{db.DatabaseName}' 
                    AND table_name = '{tableName}' 
                    AND index_name = '{indexName}'";
                
                var result = db.ExecuteScalar(checkSql);
                if (Convert.ToInt32(result) == 0)
                {
                    LogHelper.Info($"[BusinessDatabaseFixer] Adding Unique Index {indexName} to {tableName}");
                    // Using IGNORE to handle duplicate data during migration (duplicates will be dropped or errored depending on mode, usually better to let it fail or fix data first)
                    // For safety, we just try ADD UNIQUE. If data is bad, it will throw exception and not apply, which is safer than dropping data.
                    db.ExecuteNonQuery($"ALTER TABLE `{tableName}` ADD UNIQUE INDEX `{indexName}` ({columns})");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error($"[BusinessDatabaseFixer] Failed to add index {indexName}: {ex.Message}");
            }
        }

        private static bool ColumnExists(SQLHelper db, string tableName, string columnName)
        {
            try
            {
                string sql = $@"
                    SELECT COUNT(*) 
                    FROM information_schema.columns 
                    WHERE table_schema = '{db.DatabaseName}' 
                    AND table_name = '{tableName}' 
                    AND column_name = '{columnName}'";
                var result = db.ExecuteScalar(sql);
                return Convert.ToInt32(result) > 0;
            }
            catch { return false; }
        }

        private static void EnsureColumnExists(SQLHelper db, string tableName, string columnName, string definition)
        {
            if (!ColumnExists(db, tableName, columnName))
            {
                LogHelper.Info($"[BusinessDatabaseFixer] Adding column {columnName} to {tableName}");
                db.ExecuteNonQuery($"ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {definition}");
            }
        }
    }
}
