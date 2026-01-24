using System;
using System.Collections.Generic;
using System.Linq;
using MySql.Data.MySqlClient;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using System.Diagnostics;

namespace DoorMonitorSystem.Assets.Helper
{
    /// <summary>
    /// MySQL 数据库操作辅助类
    /// 提供数据库连接、表管理、CRUD操作、事务处理等功能
    /// </summary>
    public class SQLHelper : IDisposable
    {
        #region 私有字段和属性

        private MySqlConnection _connection;
        private MySqlTransaction _transaction;
        private readonly string _server;
        private readonly string _userId;
        private readonly string _password;
        private readonly string _databaseName;
        private bool _disposed = false;

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取连接状态
        /// </summary>
        public bool IsConnected => _connection?.State == ConnectionState.Open;

        /// <summary>
        /// 获取连接字符串
        /// </summary>
        public string ConnectionString => $"Server={_server};Uid={_userId};Pwd={_password};Database={_databaseName};";

        /// <summary>
        /// 获取数据库名称
        /// </summary>
        public string DatabaseName => _databaseName;

        /// <summary>
        /// 获取服务器地址
        /// </summary>
        public string Server => _server;

        /// <summary>
        /// 获取当前事务（如果有）
        /// </summary>
        public MySqlTransaction CurrentTransaction => _transaction;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="server">MySQL服务器地址</param>
        /// <param name="userId">数据库用户名</param>
        /// <param name="password">数据库密码</param>
        /// <param name="databaseName">数据库名称</param>
        public SQLHelper(string server, string userId, string password, string databaseName)
        {
            if (string.IsNullOrEmpty(server))
                throw new ArgumentException("服务器地址不能为空", nameof(server));
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("用户名不能为空", nameof(userId));
            if (string.IsNullOrEmpty(databaseName))
                throw new ArgumentException("数据库名称不能为空", nameof(databaseName));

            _server = server;
            _userId = userId;
            _password = password ?? string.Empty;
            _databaseName = databaseName;
        }

        #endregion

        #region 数据库连接管理

        /// <summary>
        /// 连接到数据库
        /// 如果数据库不存在，会自动创建
        /// </summary>
        /// <exception cref="MySqlException">MySQL相关异常</exception>
        /// <exception cref="Exception">其他连接异常</exception>
        public void Connect()
        {
            try
            {
                // 先尝试连接指定数据库
                _connection = new MySqlConnection(ConnectionString);
                _connection.Open();
                Debug.WriteLine($"成功连接到数据库: {_databaseName}");
            }
            catch (MySqlException ex) when (ex.Number == 1049) // 数据库不存在错误代码
            {
                Debug.WriteLine($"数据库 {_databaseName} 不存在，正在创建...");
                CreateDatabase();

                // 重新连接到新创建的数据库
                _connection = new MySqlConnection(ConnectionString);
                _connection.Open();
                Debug.WriteLine($"数据库 {_databaseName} 创建并连接成功");
            }
            catch (Exception ex)
            {
                throw new Exception($"连接数据库失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 断开数据库连接
        /// </summary>
        public void Disconnect()
        {
            try
            {
                // 如果有未提交的事务，则回滚
                if (_transaction != null)
                {
                    _transaction.Rollback();
                    _transaction.Dispose();
                    _transaction = null;
                    Debug.WriteLine("未提交的事务已回滚");
                }

                _connection?.Close();
                Debug.WriteLine("数据库连接已关闭");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"断开连接时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建数据库
        /// </summary>
        private void CreateDatabase()
        {
            string tempConnectionString = $"Server={_server};Uid={_userId};Pwd={_password};";

            using var tempConnection = new MySqlConnection(tempConnectionString);
            tempConnection.Open();

            using var command = new MySqlCommand(
                $"CREATE DATABASE `{_databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci",
                tempConnection
            );
            command.ExecuteNonQuery();
            Debug.WriteLine($"数据库 {_databaseName} 创建成功");
        }

        /// <summary>
        /// 检查数据库是否存在
        /// </summary>
        /// <returns>如果数据库存在返回true，否则返回false</returns>
        public bool DatabaseExists()
        {
            string tempConnectionString = $"Server={_server};Uid={_userId};Pwd={_password};";

            using var tempConnection = new MySqlConnection(tempConnectionString);
            tempConnection.Open();

            using var command = new MySqlCommand(
                "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = @databaseName",
                tempConnection
            );
            command.Parameters.AddWithValue("@databaseName", _databaseName);

            var result = command.ExecuteScalar();
            return Convert.ToInt32(result) > 0;
        }

        /// <summary>
        /// 确保数据库连接已建立
        /// </summary>
        private void EnsureConnected()
        {
            if (_connection == null || _connection.State != ConnectionState.Open)
            {
                Connect();
            }
        }

        #endregion

        #region 基本SQL操作

        /// <summary>
        /// 执行非查询SQL语句（INSERT、UPDATE、DELETE等）
        /// </summary>
        /// <param name="sql">要执行的SQL语句</param>
        /// <param name="parameters">SQL参数数组</param>
        /// <returns>受影响的行数</returns>
        public int ExecuteNonQuery(string sql, params MySqlParameter[] parameters)
        {
            EnsureConnected();

            using var command = new MySqlCommand(sql, _connection);
            if (_transaction != null)
                command.Transaction = _transaction;

            command.Parameters.AddRange(parameters);
            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// 执行查询并返回DataTable
        /// </summary>
        /// <param name="sql">要执行的SQL查询语句</param>
        /// <param name="parameters">SQL参数数组</param>
        /// <returns>包含查询结果的DataTable</returns>
        public DataTable ExecuteQuery(string sql, params MySqlParameter[] parameters)
        {
            EnsureConnected();

            using var command = new MySqlCommand(sql, _connection);
            if (_transaction != null)
                command.Transaction = _transaction;

            command.Parameters.AddRange(parameters);

            using var adapter = new MySqlDataAdapter(command);
            var dataTable = new DataTable();
            adapter.Fill(dataTable);

            return dataTable;
        }

        /// <summary>
        /// 执行查询并返回第一行第一列的值
        /// </summary>
        /// <param name="sql">要执行的SQL查询语句</param>
        /// <param name="parameters">SQL参数数组</param>
        /// <returns>查询结果的第一行第一列</returns>
        public object ExecuteScalar(string sql, params MySqlParameter[] parameters)
        {
            EnsureConnected();

            using var command = new MySqlCommand(sql, _connection);
            if (_transaction != null)
                command.Transaction = _transaction;

            command.Parameters.AddRange(parameters);
            return command.ExecuteScalar();
        }

        /// <summary>
        /// 执行查询并返回MySqlDataReader
        /// 注意：调用者需要负责关闭DataReader
        /// </summary>
        /// <param name="sql">要执行的SQL查询语句</param>
        /// <param name="parameters">SQL参数数组</param>
        /// <returns>MySqlDataReader对象</returns>
        public MySqlDataReader ExecuteReader(string sql, params MySqlParameter[] parameters)
        {
            EnsureConnected();

            var command = new MySqlCommand(sql, _connection);
            if (_transaction != null)
                command.Transaction = _transaction;

            command.Parameters.AddRange(parameters);
            return command.ExecuteReader();
        }

        #endregion

        #region 事务管理

        /// <summary>
        /// 开始事务
        /// </summary>
        public void BeginTransaction()
        {
            EnsureConnected();
            _transaction = _connection.BeginTransaction();
            Debug.WriteLine("事务开始");
        }

        /// <summary>
        /// 开始指定隔离级别的事务
        /// </summary>
        /// <param name="isolationLevel">事务隔离级别</param>
        public void BeginTransaction(IsolationLevel isolationLevel)
        {
            EnsureConnected();
            _transaction = _connection.BeginTransaction(isolationLevel);
            Debug.WriteLine($"事务开始，隔离级别: {isolationLevel}");
        }

        /// <summary>
        /// 提交事务
        /// </summary>
        public void CommitTransaction()
        {
            if (_transaction == null)
                throw new InvalidOperationException("没有活动的事务可以提交");

            _transaction.Commit();
            _transaction.Dispose();
            _transaction = null;
            Debug.WriteLine("事务已提交");
        }

        /// <summary>
        /// 回滚事务
        /// </summary>
        public void RollbackTransaction()
        {
            if (_transaction == null)
                throw new InvalidOperationException("没有活动的事务可以回滚");

            _transaction.Rollback();
            _transaction.Dispose();
            _transaction = null;
            Debug.WriteLine("事务已回滚");
        }

        #endregion

        #region 表管理

        /// <summary>
        /// 检查表是否存在
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <returns>如果表存在返回true，否则返回false</returns>
        public bool TableExists(string tableName)
        {
            string sql = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = @databaseName AND table_name = @tableName";

            var parameters = new[]
            {
            new MySqlParameter("@databaseName", _databaseName),
            new MySqlParameter("@tableName", tableName)
        };

            var result = ExecuteScalar(sql, parameters);
            return Convert.ToInt32(result) > 0;
        }

        /// <summary>
        /// 创建表（如果不存在）
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="tableDefinition">表定义SQL</param>
        public void CreateTableIfNotExists(string tableName, string tableDefinition)
        {
            string sql = $"CREATE TABLE IF NOT EXISTS `{tableName}` ({tableDefinition}) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
            ExecuteNonQuery(sql);
            Debug.WriteLine($"表 {tableName} 创建成功");
        }

        /// <summary>
        /// 删除表（如果存在）
        /// </summary>
        /// <param name="tableName">表名</param>
        public void DropTableIfExists(string tableName)
        {
            string sql = $"DROP TABLE IF EXISTS `{tableName}`";
            ExecuteNonQuery(sql);
            Debug.WriteLine($"表 {tableName} 删除成功");
        }

        /// <summary>
        /// 获取所有表名
        /// </summary>
        /// <returns>表名列表</returns>
        public List<string> GetTableNames()
        {
            var tableNames = new List<string>();
            string sql = "SELECT table_name FROM information_schema.tables WHERE table_schema = @databaseName";

            var parameters = new[]
            {
            new MySqlParameter("@databaseName", _databaseName)
        };

            var dataTable = ExecuteQuery(sql, parameters);
            foreach (DataRow row in dataTable.Rows)
            {
                tableNames.Add(row["table_name"].ToString());
            }

            return tableNames;
        }

        /// <summary>
        /// 获取表的列信息
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <returns>包含列信息的DataTable</returns>
        public DataTable GetTableColumns(string tableName)
        {
            string sql = @"SELECT 
                        column_name AS '列名',
                        data_type AS '数据类型',
                        is_nullable AS '是否可为空',
                        column_default AS '默认值',
                        column_key AS '键类型',
                        extra AS '额外信息'
                    FROM information_schema.columns 
                    WHERE table_schema = @databaseName AND table_name = @tableName
                    ORDER BY ordinal_position";

            var parameters = new[]
            {
            new MySqlParameter("@databaseName", _databaseName),
            new MySqlParameter("@tableName", tableName)
        };

            return ExecuteQuery(sql, parameters);
        }

        #endregion

        #region 基于模型的表操作

        /// <summary>
        /// 根据C#模型类创建数据库表
        /// 支持数据注解：Table, Column, Key, Required, MaxLength, StringLength, DatabaseGenerated, NotMapped
        /// Table：指定类映射到的数据库表名。 
        /// Column：指定属性映射到的数据库列名及其类型。 
        ///Key：将属性标识为主键。 
        ///DatabaseGenerated：指定数据库生成属性值的方式（例如自增）。 
        ///Required：表示该属性是必须的，不能为null。 
        ///MaxLength / StringLength：指定字符串属性的最大长度。 
        ///NotMapped：表示属性不被映射到数据库。
        /// </summary>
        /// <typeparam name="T">模型类型</typeparam>
        public void CreateTableFromModel<T>()
        {
            var type = typeof(T);
            var tableName = GetTableName(type);
            var properties = type.GetProperties();

            var columns = new List<string>();
            var primaryKeys = new List<string>();
            var foreignKeys = new List<string>();

            foreach (var property in properties)
            {
                // 检查是否被忽略
                if (property.GetCustomAttribute<NotMappedAttribute>() != null)
                    continue;

                var columnDefinition = GetColumnDefinition(property);
                if (!string.IsNullOrEmpty(columnDefinition))
                {
                    columns.Add(columnDefinition);

                    // 检查主键
                    if (property.GetCustomAttribute<KeyAttribute>() != null)
                    {
                        primaryKeys.Add(GetColumnName(property));
                    }

                    // 检查外键
                    var foreignKeyAttr = property.GetCustomAttribute<ForeignKeyAttribute>();
                    if (foreignKeyAttr != null)
                    {
                        foreignKeys.Add(GetForeignKeyDefinition(property, foreignKeyAttr));
                    }
                }
            }

            // 构建创建表的SQL
            var createTableSql = BuildCreateTableSql(tableName, columns, primaryKeys, foreignKeys);
            ExecuteNonQuery(createTableSql);

            Debug.WriteLine($"表 {tableName} 创建成功");
        }

        /// <summary>
        /// 获取表名（支持Table特性）
        /// </summary>
        private string GetTableName(Type type)
        {
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            return tableAttr?.Name ?? type.Name;
        }

        /// <summary>
        /// 获取列名（支持Column特性）
        /// </summary>
        private string GetColumnName(PropertyInfo property)
        {
            var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
            return columnAttr?.Name ?? property.Name;
        }

        /// <summary>
        /// 获取列定义
        /// </summary> 
        private string GetColumnDefinition(PropertyInfo property)
        {
            var columnName = GetColumnName(property);
            var sqlType = GetSqlType(property.PropertyType);

            if (sqlType == null) return null;

            // 处理字符串长度
            if (property.PropertyType == typeof(string))
            {
                var maxLengthAttr = property.GetCustomAttribute<MaxLengthAttribute>();
                var stringLengthAttr = property.GetCustomAttribute<StringLengthAttribute>();

                if (maxLengthAttr != null)
                {
                    sqlType = $"VARCHAR({maxLengthAttr.Length})";
                }
                else if (stringLengthAttr != null)
                {
                    sqlType = $"VARCHAR({stringLengthAttr.MaximumLength})";
                }
            }

            // 处理必填字段
            var requiredAttr = property.GetCustomAttribute<RequiredAttribute>();
            var isNullable = IsNullable(property) && requiredAttr == null;

            if (!isNullable)
            {
                sqlType += sqlType.StartsWith("VARCHAR") || sqlType == "TEXT" ? " NOT NULL" : " NOT NULL";
            }

            // 处理自增
            var databaseGeneratedAttr = property.GetCustomAttribute<DatabaseGeneratedAttribute>();
            if (databaseGeneratedAttr?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity)
            {
                sqlType += " AUTO_INCREMENT";
            }

            // 处理 Range 特性
            var rangeAttr = property.GetCustomAttribute<RangeAttribute>();
            if (rangeAttr != null)
            {
                sqlType += $" CHECK ({columnName} >= {rangeAttr.Minimum} AND {columnName} <= {rangeAttr.Maximum})";
            }

            return $"{columnName} {sqlType}";
        }


        /// <summary>
        /// 获取SQL类型映射
        /// </summary>
        private string GetSqlType(Type type)
        {
            // 处理可空类型
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                type = Nullable.GetUnderlyingType(type);
            }

            if (type.IsEnum)
            {
                return "INT";
            }

            var typeMapping = new Dictionary<Type, string>
        {
            { typeof(int), "INT" },
            { typeof(long), "BIGINT" },
            { typeof(short), "SMALLINT" },
            { typeof(byte), "TINYINT" },
            { typeof(bool), "TINYINT(1)" },
            { typeof(string), "VARCHAR(255)" },
            { typeof(DateTime), "DATETIME" },
            { typeof(DateTimeOffset), "DATETIME" },
            { typeof(TimeSpan), "TIME" },
            { typeof(decimal), "DECIMAL(18,2)" },
            { typeof(double), "DOUBLE" },
            { typeof(float), "FLOAT" },
            { typeof(Guid), "CHAR(36)" },
            { typeof(byte[]), "BLOB" }
        };

            return typeMapping.TryGetValue(type, out string? sqlType) ? sqlType : null;
        }

        /// <summary>
        /// 检查属性是否可为空
        /// </summary>
        private bool IsNullable(PropertyInfo property)
        {
            // 对于引用类型，默认可为空
            if (!property.PropertyType.IsValueType)
                return true;

            // 对于值类型，检查是否是Nullable<T>
            return property.PropertyType.IsGenericType &&
                   property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        /// <summary>
        /// 获取外键定义
        /// </summary>
        private string GetForeignKeyDefinition(PropertyInfo property, ForeignKeyAttribute foreignKeyAttr)
        {
            var columnName = GetColumnName(property);
            // 简化实现：假设外键引用表名为属性类型名，引用列名为"Id"
            var referencedTable = property.PropertyType.Name;
            var referencedColumn = "Id";

            return $"FOREIGN KEY ({columnName}) REFERENCES {referencedTable}({referencedColumn})";
        }

        /// <summary>
        /// 构建创建表的SQL语句
        /// </summary>
        private string BuildCreateTableSql(string tableName, List<string> columns, List<string> primaryKeys, List<string> foreignKeys)
        {
            var sqlParts = new List<string>();

            // 添加列定义
            sqlParts.AddRange(columns);

            // 添加主键约束
            if (primaryKeys.Count > 0)
            {
                sqlParts.Add($"PRIMARY KEY ({string.Join(", ", primaryKeys)})");
            }

            // 添加外键约束
            sqlParts.AddRange(foreignKeys);

            return $"CREATE TABLE IF NOT EXISTS `{tableName}` ({string.Join(", ", sqlParts)}) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
        }

        #endregion

        #region 基于模型的CRUD操作

        /// <summary>
        /// 插入实体到数据库
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="entity">要插入的实体</param>
        /// <returns>受影响的行数</returns>
        public int Insert<T>(T entity)
        {
            var type = typeof(T);
            var tableName = GetTableName(type);
            var properties = type.GetProperties();

            var columns = new List<string>();
            var parameters = new List<MySqlParameter>();
            var values = new List<string>();

            foreach (var property in properties)
            {
                // 1. NotMapped
                if (property.GetCustomAttribute<NotMappedAttribute>() != null)
                    continue;

                // 2. Identity
                var dbGenAttr = property.GetCustomAttribute<DatabaseGeneratedAttribute>();
                if (dbGenAttr?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity)
                    continue;

                // 3. 只允许简单类型
                if (!IsSimpleType(property.PropertyType))
                    continue;

                var columnName = GetColumnName(property);
                var value = property.GetValue(entity);

                columns.Add(columnName);
                values.Add($"@{columnName}");
                parameters.Add(
                    new MySqlParameter($"@{columnName}", value ?? DBNull.Value)
                );
            }

            if (columns.Count == 0)
                throw new InvalidOperationException(
                    $"实体 {type.Name} 没有可插入的字段");

            var sql = $"INSERT INTO `{tableName}` " +
                      $"({string.Join(", ", columns)}) " +
                      $"VALUES ({string.Join(", ", values)})";

            return ExecuteNonQuery(sql, parameters.ToArray());
        }

        private bool IsSimpleType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(DateTime)
                || type == typeof(decimal)
                || type == typeof(Guid);
        }


        /// <summary>
        /// 查询所有实体
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <returns>实体列表</returns>
        public List<T> SelectAll<T>() where T : new()
        {
            var type = typeof(T);
            var tableName = GetTableName(type);
            var sql = $"SELECT * FROM `{tableName}`";

            var dataTable = ExecuteQuery(sql);
            var result = new List<T>();

            foreach (DataRow row in dataTable.Rows)
            {
                var entity = new T();
                foreach (var property in type.GetProperties())
                {
                    if (property.GetCustomAttribute<NotMappedAttribute>() != null)
                        continue;

                    var columnName = GetColumnName(property);
                    if (dataTable.Columns.Contains(columnName) && row[columnName] != DBNull.Value)
                    {
                        var value = row[columnName];
                        var targetType = property.PropertyType;

                        // 处理可空类型
                        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        {
                            targetType = Nullable.GetUnderlyingType(targetType);
                        }

                        if (targetType.IsEnum)
                        {
                            // Handle Enum conversion from int/string
                            value = Enum.ToObject(targetType, value);
                        }
                        else
                        {
                            value = Convert.ChangeType(value, targetType);
                        }

                        property.SetValue(entity, value);
                    }
                }
                result.Add(entity);
            }

            return result;
        }

        /// <summary>
        /// 查找所有实体 (别名 SelectAll)
        /// </summary>
        public List<T> FindAll<T>() where T : new()
        {
            return SelectAll<T>();
        }

        /// <summary>
        /// 根据条件查找实体
        /// </summary>
        /// <param name="whereClause">WHERE子句，例如 "Id = @id AND Name = @name"</param>
        /// <param name="parameters">参数列表</param>
        public List<T> FindAll<T>(string whereClause, params MySqlParameter[] parameters) where T : new()
        {
            var type = typeof(T);
            var tableName = GetTableName(type);
            var sql = $"SELECT * FROM `{tableName}` WHERE {whereClause}";

            var dataTable = ExecuteQuery(sql, parameters);
            var result = new List<T>();

            foreach (DataRow row in dataTable.Rows)
            {
                var entity = new T();
                foreach (var property in type.GetProperties())
                {
                    if (property.GetCustomAttribute<NotMappedAttribute>() != null)
                        continue;

                    var columnName = GetColumnName(property);
                    if (dataTable.Columns.Contains(columnName) && row[columnName] != DBNull.Value)
                    {
                        var value = row[columnName];
                        var targetType = property.PropertyType;

                        // 处理可空类型
                        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        {
                            targetType = Nullable.GetUnderlyingType(targetType);
                        }

                        if (targetType.IsEnum)
                        {
                            // Handle Enum conversion from int/string
                            value = Enum.ToObject(targetType, value);
                        }
                        else
                        {
                            value = Convert.ChangeType(value, targetType);
                        }

                        property.SetValue(entity, value);
                    }
                }
                result.Add(entity);
            }

            return result;
        }

        /// <summary>
        /// 根据主键查询实体
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="id">主键值</param>
        /// <returns>找到的实体，如果未找到返回null</returns>
        public T SelectById<T>(object id) where T : new()
        {
            var type = typeof(T);
            var tableName = GetTableName(type);

            // 查找主键属性
            PropertyInfo keyProperty = null;
            foreach (var property in type.GetProperties())
            {
                if (property.GetCustomAttribute<KeyAttribute>() != null)
                {
                    keyProperty = property;
                    break;
                }
            }

            if (keyProperty == null)
                throw new InvalidOperationException($"类型 {type.Name} 没有定义主键属性");

            var keyColumnName = GetColumnName(keyProperty);
            var sql = $"SELECT * FROM `{tableName}` WHERE {keyColumnName} = @id";

            var parameters = new[]
            {
            new MySqlParameter("@id", id)
        };

            var dataTable = ExecuteQuery(sql, parameters);
            if (dataTable.Rows.Count == 0)
                return default(T);

            var row = dataTable.Rows[0];
            var entity = new T();

            foreach (var property in type.GetProperties())
            {
                if (property.GetCustomAttribute<NotMappedAttribute>() != null)
                    continue;

                var columnName = GetColumnName(property);
                if (dataTable.Columns.Contains(columnName) && row[columnName] != DBNull.Value)
                {
                    var value = row[columnName];
                    var targetType = property.PropertyType;

                    // 处理可空类型
                    if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        targetType = Nullable.GetUnderlyingType(targetType);
                    }

                    if (targetType.IsEnum)
                    {
                        // Handle Enum conversion from int/string
                        value = Enum.ToObject(targetType, value);
                    }
                    else
                    {
                        value = Convert.ChangeType(value, targetType);
                    }

                    property.SetValue(entity, value);
                }
            }

            return entity;
        }

        /// <summary>
        /// 更新实体
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="entity">要更新的实体</param>
        /// <returns>受影响的行数</returns>
        public int Update<T>(T entity)
        {
            var type = typeof(T);
            var tableName = GetTableName(type);
            var properties = type.GetProperties();

            // 查找主键属性
            PropertyInfo keyProperty = null;
            foreach (var property in properties)
            {
                if (property.GetCustomAttribute<KeyAttribute>() != null)
                {
                    keyProperty = property;
                    break;
                }
            }

            if (keyProperty == null)
                throw new InvalidOperationException($"类型 {type.Name} 没有定义主键属性");

            var setClauses = new List<string>();
            var parameters = new List<MySqlParameter>();

            foreach (var property in properties)
            {
                // 跳过忽略的属性、主键和自增字段
                if (property.GetCustomAttribute<NotMappedAttribute>() != null)
                    continue;

                if (property == keyProperty)
                    continue;

                var dbGenAttr = property.GetCustomAttribute<DatabaseGeneratedAttribute>();
                if (dbGenAttr?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity)
                    continue;

                var columnName = GetColumnName(property);
                var value = property.GetValue(entity);

                setClauses.Add($"{columnName} = @{columnName}");
                parameters.Add(new MySqlParameter($"@{columnName}", value ?? DBNull.Value));
            }

            var keyColumnName = GetColumnName(keyProperty);
            var keyValue = keyProperty.GetValue(entity);
            parameters.Add(new MySqlParameter($"@keyValue", keyValue));

            var sql = $"UPDATE `{tableName}` SET {string.Join(", ", setClauses)} WHERE {keyColumnName} = @keyValue";
            return ExecuteNonQuery(sql, parameters.ToArray());
        }

        /// <summary>
        /// 删除实体
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="entity">要删除的实体</param>
        /// <returns>受影响的行数</returns>
        public int Delete<T>(T entity)
        {
            var type = typeof(T);
            var tableName = GetTableName(type);

            // 查找主键属性
            PropertyInfo keyProperty = null;
            foreach (var property in type.GetProperties())
            {
                if (property.GetCustomAttribute<KeyAttribute>() != null)
                {
                    keyProperty = property;
                    break;
                }
            }

            if (keyProperty == null)
                throw new InvalidOperationException($"类型 {type.Name} 没有定义主键属性");

            var keyColumnName = GetColumnName(keyProperty);
            var keyValue = keyProperty.GetValue(entity);

            var sql = $"DELETE FROM `{tableName}` WHERE {keyColumnName} = @keyValue";
            var parameters = new[]
            {
            new MySqlParameter("@keyValue", keyValue)
        };

            return ExecuteNonQuery(sql, parameters);
        }

        #endregion

        #region 数据库信息

        /// <summary>
        /// 获取数据库版本信息
        /// </summary>
        /// <returns>数据库版本字符串</returns>
        public string GetDatabaseVersion()
        {
            var result = ExecuteScalar("SELECT VERSION()");
            return result?.ToString() ?? "未知";
        }

        /// <summary>
        /// 获取数据库大小（MB）
        /// </summary>
        /// <returns>数据库大小（MB）</returns>
        public decimal GetDatabaseSize()
        {
            string sql = @"
            SELECT 
                ROUND(SUM(data_length + index_length) / 1024 / 1024, 2) AS 'size_mb'
            FROM information_schema.tables 
            WHERE table_schema = @databaseName";

            var parameters = new[]
            {
            new MySqlParameter("@databaseName", _databaseName)
        };

            var result = ExecuteScalar(sql, parameters);
            return result == DBNull.Value ? 0 : Convert.ToDecimal(result);
        }

        #endregion

        #region IDisposable 实现

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    _transaction?.Dispose();
                    _connection?.Dispose();
                }

                // 释放非托管资源
                _transaction = null;
                _connection = null;
                _disposed = true;
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~SQLHelper()
        {
            Dispose(false);
        }

        #endregion
    }
}
