using DoorMonitorSystem.Base;
using DoorMonitorSystem.Models;
using DoorMonitorSystem.Assets.Helper;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections.Generic;
using Base;
using System.Linq;
using DoorMonitorSystem.Models.ConfigEntity;

namespace DoorMonitorSystem.ViewModels
{
    public class SystemLogViewModel : NotifyPropertyChanged
    {
        private DateTime _startDate = DateTime.Now;
        private DateTime _endDate = DateTime.Now;
        private string _keyword = "";
        private bool _isLoading;
        private List<PointLogEntity> _allLogsCache = new List<PointLogEntity>();
        private System.Threading.CancellationTokenSource? _backgroundCts;

        public DateTime StartDate
        {
            get => _startDate;
            set { _startDate = value; OnPropertyChanged(); OnPropertyChanged(nameof(StartFullDateTime)); }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set { _endDate = value; OnPropertyChanged(); OnPropertyChanged(nameof(EndFullDateTime)); }
        }

        public string Keyword
        {
            get => _keyword;
            set { _keyword = value; OnPropertyChanged(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        // 分类筛选
        public ObservableCollection<string> Categories { get; set; } = new ObservableCollection<string>();
        
        private string _selectedCategory = "全部";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set { _selectedCategory = value; OnPropertyChanged(); }
        }

        // 记录等级筛选 (1=状态, 2=报警)
        public List<string> LogLevelOptions { get; set; } = new List<string> { "全部", "状态", "报警" };

        private string _selectedLogLevel = "全部";
        public string SelectedLogLevel
        {
            get => _selectedLogLevel;
            set
            {
                _selectedLogLevel = value;
                OnPropertyChanged();
                CurrentPage = 1;
                _ = LoadLogsAsync();
            }
        }

        // 日志表类型切换
        public List<string> LogTableTypes { get; set; } = new List<string> { "系统日志", "模拟量数据" };
        
        private string _selectedTableType = "系统日志";
        public string SelectedTableType
        {
            get => _selectedTableType;
            set 
            { 
                _selectedTableType = value; 
                OnPropertyChanged();
                CurrentPage = 1;
                _ = LoadLogsAsync();
            }
        }

        private bool _hasAnalogPoints = false;
        public bool HasAnalogPoints
        {
            get => _hasAnalogPoints;
            set { _hasAnalogPoints = value; OnPropertyChanged(); }
        }

        public ObservableCollection<PointLogEntity> Logs { get; set; } = new ObservableCollection<PointLogEntity>();

        // 分页属性
        private int _currentPage = 1;
        private int _totalPages = 1;
        private int _totalCount = 0;
        private int _pageSize = 1000;

        public int CurrentPage
        {
            get => _currentPage;
            set { _currentPage = value; OnPropertyChanged(); }
        }

        public int TotalPages
        {
            get => _totalPages;
            set { _totalPages = value; OnPropertyChanged(); }
        }

        public int TotalCount
        {
            get => _totalCount;
            set { _totalCount = value; OnPropertyChanged(); }
        }
        
        // 时间筛选扩充
        private string _searchStartTime = "00:00:00";
        private string _searchEndTime = DateTime.Now.ToString("HH:mm:ss");

        public string SearchStartTime
        {
            get => _searchStartTime;
            set { _searchStartTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(StartFullDateTime)); }
        }

        public string SearchEndTime
        {
            get => _searchEndTime;
            set { _searchEndTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(EndFullDateTime)); }
        }

        public DateTime StartFullDateTime
        {
            get 
            {
                if (TimeSpan.TryParse(SearchStartTime, out var ts))
                    return StartDate.Date.Add(ts);
                return StartDate.Date;
            }
            set
            {
                StartDate = value.Date;
                SearchStartTime = value.ToString("HH:mm:ss");
                
                // 互锁逻辑：开始时间推着结束时间走
                if (value > EndFullDateTime)
                {
                    EndFullDateTime = value.AddHours(1); // 默认顺延一小时，或根据需要调整
                }
                
                OnPropertyChanged();
            }
        }

        public DateTime EndFullDateTime
        {
            get
            {
                if (TimeSpan.TryParse(SearchEndTime, out var ts))
                    return EndDate.Date.Add(ts);
                return EndDate.Date;
            }
            set
            {
                EndDate = value.Date;
                SearchEndTime = value.ToString("HH:mm:ss");

                // 互锁逻辑：结束时间拉着开始时间走
                if (value < StartFullDateTime)
                {
                    StartFullDateTime = value.AddHours(-1); // 默认提前一小时，或根据需要调整
                }

                OnPropertyChanged();
            }
        }

        public ICommand SearchCommand { get; set; }
        public ICommand PrevPageCommand { get; set; }
        public ICommand NextPageCommand { get; set; }
        public ICommand FirstPageCommand { get; set; } // 首页
        public ICommand ResetCommand { get; set; }
        public ICommand ExportCommand { get; set; }

        public SystemLogViewModel()
        {
            SearchCommand = new RelayCommand(OnSearch);
            PrevPageCommand = new RelayCommand(OnPrevPage);
            NextPageCommand = new RelayCommand(OnNextPage);
            ResetCommand = new RelayCommand(OnReset);
            ExportCommand = new RelayCommand(OnExport);

            ExportCommand = new RelayCommand(OnExport);

            // 加载分类
            LoadCategories();

            // 默认加载
            _ = LoadLogsAsync();
        }

        private void OnSearch(object obj)
        {
            CurrentPage = 1;
            _ = LoadLogsAsync();
        }

        private void OnPrevPage(object obj)
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                _ = UpdateDisplayLogsAsync();
            }
        }

        private void OnNextPage(object obj)
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                _ = UpdateDisplayLogsAsync();
            }
        }

        /// <summary>
        /// 智能刷新显示：优先使用缓存，若缓存未到位则实时查询
        /// </summary>
        private async Task UpdateDisplayLogsAsync()
        {
            int skip = (CurrentPage - 1) * _pageSize;
            
            // 检查缓存中是否已有当前页数据
            lock (_allLogsCache)
            {
                if (_allLogsCache.Count >= skip + 1)
                {
                    Logs.Clear();
                    var pageData = _allLogsCache.Skip(skip).Take(_pageSize).ToList();
                    int index = skip + 1;
                    foreach (var log in pageData)
                    {
                        log.RowIndex = index++;
                        Logs.Add(log);
                    }
                    return;
                }
            }

            // 缓存未命中（后台还没加载到这一页），执行单页查询
            IsLoading = true;
            try
            {
                await Task.Run(() =>
                {
                    if (GlobalData.SysCfg == null) return;
                    string logDb = GlobalData.SysCfg.LogDatabaseName;
                    using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, logDb);
                    db.Connect();

                    // 这里的 SQL 逻辑需要跟 LoadLogs 里的 meta 逻辑保持一致
                    // 为了简化，这里我们调用一个内部的构建 SQL 的方法（如果逻辑复杂的话）
                    // 暂且直接复制关键查询逻辑
                    var (fromSource, baseWhere) = GetSearchCriteria();
                    if (string.IsNullOrEmpty(fromSource)) return;

                    string sql = $"SELECT * FROM {fromSource} WHERE {baseWhere} ORDER BY LogTime DESC LIMIT {skip}, {_pageSize}";
                    var result = db.Query<PointLogEntity>(sql);

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Logs.Clear();
                        int index = skip + 1;
                        foreach (var log in result)
                        {
                            log.RowIndex = index++;
                            Logs.Add(log);
                        }
                    });
                });
            }
            finally { IsLoading = false; }
        }

        /// <summary>
        /// 辅助方法：构建查询条件
        /// </summary>
        private (string fromSource, string baseWhere) GetSearchCriteria()
        {
            if (GlobalData.SysCfg == null) return ("", "");
            
            string startDt = $"{StartDate:yyyy-MM-dd} {SearchStartTime}";
            string endDt = $"{EndDate:yyyy-MM-dd} {SearchEndTime}";

            List<string> tableList = new();
            DateTime d = new DateTime(StartDate.Year, StartDate.Month, 1);
            string tablePrefix = SelectedTableType == "模拟量数据" ? "AnalogLogs" : "PointLogs";
            
            // 这里需要一个新的数据库连接来检查 TableExists
            using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.LogDatabaseName);
            db.Connect();

            while (d <= EndDate)
            {
                string tableName = $"{tablePrefix}_{d:yyyyMM}";
                if (db.TableExists(tableName)) tableList.Add(tableName);
                d = d.AddMonths(1);
            }

            if (tableList.Count == 0) return ("", "");

            string fromSource = tableList.Count == 1 
                ? $"`{tableList[0]}`" 
                : $"(" + string.Join(" UNION ALL ", tableList.Select(t => $"SELECT * FROM `{t}`")) + ") AS CombinedLogs";

            string baseWhere = $"LogTime BETWEEN '{startDt}' AND '{endDt}'";
            if (!string.IsNullOrWhiteSpace(Keyword))
            {
                baseWhere += $" AND (Message LIKE '%{Keyword}%' OR Address LIKE '%{Keyword}%' OR ValText LIKE '%{Keyword}%')";
            }
            if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "全部")
            {
                baseWhere += $" AND Category = '{SelectedCategory}'";
            }
            if (SelectedLogLevel == "状态") baseWhere += " AND LogType <> 2";
            else if (SelectedLogLevel == "报警") baseWhere += " AND LogType = 2";

            return (fromSource, baseWhere);
        }

        private void OnReset(object obj)
        {
            StartDate = DateTime.Now;
            EndDate = DateTime.Now;
            SearchStartTime = "00:00:00";
            SearchEndTime = DateTime.Now.ToString("HH:mm:ss");
            Keyword = "";
            SelectedCategory = "全部";
            SelectedLogLevel = "全部";
            
            // 内部字段赋值以避免触发多次 LoadLogsAsync
            _selectedTableType = "系统日志"; 
            OnPropertyChanged(nameof(SelectedTableType));
            
            CurrentPage = 1;
            _ = LoadLogsAsync();
        }

        private async void OnExport(object obj)
        {
            if (TotalCount == 0)
            {
                System.Windows.MessageBox.Show("当前查询结果为空，无需导出。", "导出提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Filter = "Excel表格文件 (*.xlsx)|*.xlsx|CSV表格文件 (*.csv)|*.csv";
            string prefix = SelectedTableType == "模拟量数据" ? "模拟量数据" : "系统日志";
            saveFileDialog.FileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            
            if (saveFileDialog.ShowDialog() == true)
            {
                IsLoading = true;
                string filePath = saveFileDialog.FileName;
                bool isExcel = filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);
                
                try
                {
                    await Task.Run(() =>
                    {
                        if (GlobalData.SysCfg == null) return;
                        
                        string logDb = GlobalData.SysCfg.LogDatabaseName;
                        using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, logDb);
                        db.Connect();

                        // 1. 判定表名集合
                        List<string> tableList = new();
                        DateTime d = new DateTime(StartDate.Year, StartDate.Month, 1);
                        string tablePrefix = SelectedTableType == "模拟量数据" ? "AnalogLogs" : "PointLogs";
                        while (d <= EndDate)
                        {
                            string tableName = $"{tablePrefix}_{d:yyyyMM}";
                            if (db.TableExists(tableName)) tableList.Add(tableName);
                            d = d.AddMonths(1);
                        }
                        
                        if (tableList.Count == 0) return;

                        string fromSource = tableList.Count == 1 
                            ? $"`{tableList[0]}`" 
                            : $"(" + string.Join(" UNION ALL ", tableList.Select(t => $"SELECT * FROM `{t}`")) + ") AS CombinedLogs";

                        string startDt = $"{StartDate:yyyy-MM-dd} {SearchStartTime}";
                        string endDt = $"{EndDate:yyyy-MM-dd} {SearchEndTime}";
                        string baseWhere = $"LogTime BETWEEN '{startDt}' AND '{endDt}'";
                        if (!string.IsNullOrWhiteSpace(Keyword))
                        {
                            baseWhere += $" AND (Message LIKE '%{Keyword}%' OR Address LIKE '%{Keyword}%' OR ValText LIKE '%{Keyword}%')";
                        }

                        if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "全部")
                        {
                            baseWhere += $" AND Category = '{SelectedCategory}'";
                        }

                        if (SelectedLogLevel == "状态")
                        {
                            baseWhere += " AND LogType <> 2";
                        }
                        else if (SelectedLogLevel == "报警")
                        {
                            baseWhere += " AND LogType = 2";
                        }

                        // 2. 查询所有匹配项
                        string sql = $"SELECT * FROM {fromSource} WHERE {baseWhere} ORDER BY LogTime DESC";
                        var result = db.Query<PointLogEntity>(sql);

                        // 3. 执行导出
                        if (isExcel)
                        {
                            // 转换为具有中文名称的可读对象列表
                            var exportData = result.Select(x => new
                            {
                                时间 = x.LogTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                类型 = x.LogTypeDisplay,
                                设备ID = x.DeviceID,
                                地址 = x.Address,
                                状态 = x.ValDisplay,
                                消息内容 = x.Message
                            });
                            MiniExcelLibs.MiniExcel.SaveAs(filePath, exportData);
                        }
                        else
                        {
                            // 保留 CSV 导出逻辑作为备选
                            using (var sw = new System.IO.StreamWriter(filePath, false, System.Text.Encoding.UTF8))
                            {
                                sw.Write('\uFEFF');
                                sw.WriteLine("时间,类型,设备ID,地址,状态值,日志消息");
                                foreach (var log in result)
                                {
                                    sw.WriteLine($"{log.LogTime:yyyy-MM-dd HH:mm:ss.fff},{log.LogTypeDisplay},{log.DeviceID},{EscapeCsv(log.Address ?? "")},{log.ValDisplay},{EscapeCsv(log.Message ?? "")}");
                                }
                            }
                        }
                    });

                    System.Windows.MessageBox.Show("日志数据已成功导出！", "导出成功", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    LogHelper.Error("Export Logs Task Failed", ex);
                    System.Windows.MessageBox.Show($"导出过程中发生错误: {ex.Message}", "导出失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        private string EscapeCsv(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            // 如果包含逗号、双引号或换行符，则需要包围双引号
            if (text.Contains(",") || text.Contains("\"") || text.Contains("\r") || text.Contains("\n"))
            {
                return $"\"{text.Replace("\"", "\"\"")}\"";
            }
            return text;
        }

        private async Task LoadLogsAsync()
        {
            if (IsLoading) return;

            // 取消之前的后台加载任务
            _backgroundCts?.Cancel();
            _backgroundCts = new System.Threading.CancellationTokenSource();
            var token = _backgroundCts.Token;

            if ((EndDate.Date - StartDate.Date).TotalDays > 7)
            {
                System.Windows.MessageBox.Show("单次查询跨度不能超过 7 天，请缩小时间范围。", "查询限制", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            IsLoading = true;
            lock (_allLogsCache) _allLogsCache.Clear();
            Logs.Clear();

            try
            {
                if (GlobalData.SysCfg != null)
                {
                    await Task.Run(() =>
                    {
                        var (fromSource, baseWhere) = GetSearchCriteria();
                        if (string.IsNullOrEmpty(fromSource)) return;

                        string logDb = GlobalData.SysCfg.LogDatabaseName;
                        using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, logDb);
                        db.Connect();

                        // 1. 先查总数
                        string countSql = $"SELECT COUNT(*) FROM {fromSource} WHERE {baseWhere}";
                        object countResult = db.ExecuteScalar(countSql);
                        int total = 0;
                        if (countResult != null) int.TryParse(countResult.ToString(), out total);

                        // 2. 计算分页总数
                        int pages = (int)Math.Ceiling((double)total / _pageSize);
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            TotalCount = total;
                            TotalPages = pages < 1 ? 1 : pages;
                            CurrentPage = 1;
                        });

                        // 3. 立即抓取第一页 (Priority Load)
                        string firstPageSql = $"SELECT * FROM {fromSource} WHERE {baseWhere} ORDER BY LogTime DESC LIMIT 0, {_pageSize}";
                        var firstPageData = db.Query<PointLogEntity>(firstPageSql);
                        
                        lock (_allLogsCache) _allLogsCache.AddRange(firstPageData);

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            int index = 1;
                            foreach (var log in firstPageData)
                            {
                                log.RowIndex = index++;
                                Logs.Add(log);
                            }
                        });

                        // 4. 开启后台静默加载任务 (批量抓取剩余数据)
                        if (total > _pageSize)
                        {
                            _ = Task.Run(() => BackgroundBatchLoad(fromSource, baseWhere, total, token));
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("Search Logs Failed", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 后台批量加载逻辑：每 5000 条一组拉取
        /// </summary>
        private async Task BackgroundBatchLoad(string fromSource, string baseWhere, int totalCount, System.Threading.CancellationToken token)
        {
            try
            {
                int loaded = _pageSize; 
                int batchSize = 5000;
                string logDb = GlobalData.SysCfg?.LogDatabaseName ?? "";
                
                while (loaded < totalCount)
                {
                    if (token.IsCancellationRequested) return;

                    await Task.Delay(100); // 给数据库一点喘息时间
                    
                    using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, logDb);
                    db.Connect();

                    string sql = $"SELECT * FROM {fromSource} WHERE {baseWhere} ORDER BY LogTime DESC LIMIT {loaded}, {batchSize}";
                    var batch = db.Query<PointLogEntity>(sql);
                    
                    if (batch == null || batch.Count == 0) break;

                    lock (_allLogsCache)
                    {
                        _allLogsCache.AddRange(batch);
                    }
                    loaded += batch.Count;
                    
                    // debug: System.Diagnostics.Debug.WriteLine($"[LogService] Background loaded {loaded}/{totalCount}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("Background Log Loading Failed", ex);
            }
        }

        private void LoadCategories()
        {
            try
            {
                Categories.Clear();
                Categories.Add("全部");

                if (GlobalData.SysCfg != null)
                {
                    using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    db.Connect();
                    
                    // 从点位配置表中获取所有已配置的分类
                    string sql = "SELECT DISTINCT Category FROM DevicePointConfig WHERE Category IS NOT NULL AND Category != ''";
                    var dt = db.ExecuteQuery(sql);
                    
                    foreach (System.Data.DataRow row in dt.Rows)
                    {
                        string cat = row["Category"].ToString();
                        if (!Categories.Contains(cat)) { Categories.Add(cat); }
                    }

                    // 检查是否存在开启了记录功能的模拟量点位 (排除 bool/bit 类型，且 IsLogEnabled = 1)
                    // 使用 LOWER() 确保大小写兼容
                    var aDt = db.ExecuteQuery("SELECT COUNT(*) FROM DevicePointConfig WHERE LOWER(DataType) NOT LIKE '%bool%' AND LOWER(DataType) NOT LIKE '%bit%' AND IsLogEnabled = 1");
                    if (aDt.Rows.Count > 0)
                    {
                        HasAnalogPoints = Convert.ToInt32(aDt.Rows[0][0]) > 0;
                    }

                    // --- Schema Patch for Log DB ---
                    Task.Run(() => 
                    {
                        try 
                        {
                            string logDb = GlobalData.SysCfg.LogDatabaseName;
                            using var ldb = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, logDb);
                            ldb.Connect();
                            
                            // 补丁：同时检查 PointLogs 和 AnalogLogs 两类表
                            string[] prefixes = { "PointLogs", "AnalogLogs" };
                            foreach (var pfx in prefixes)
                            {
                                var tables = ldb.ExecuteQuery($"SHOW TABLES LIKE '{pfx}_%'");
                                foreach (System.Data.DataRow r in tables.Rows)
                                {
                                    string tName = r[0].ToString();
                                    
                                    // Check & Add Category
                                    try { ldb.ExecuteNonQuery($"ALTER TABLE `{tName}` ADD COLUMN Category VARCHAR(50);"); } catch { }
                                    // Check & Add UserName
                                    try { ldb.ExecuteNonQuery($"ALTER TABLE `{tName}` ADD COLUMN UserName VARCHAR(50);"); } catch { }
                                    // Check & Add ValText
                                    try { ldb.ExecuteNonQuery($"ALTER TABLE `{tName}` ADD COLUMN ValText VARCHAR(50);"); } catch { }
                                }
                            }
                        }
                        catch (Exception ex) 
                        {
                            LogHelper.Error("Log Schema Patch Failed", ex);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("Load Categories Failed", ex);
            }
        }
    }
}
