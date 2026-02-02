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
using DoorMonitorSystem.Models.ConfigEntity.Log;

namespace DoorMonitorSystem.ViewModels
{
    /// <summary>
    /// 系统日志视图模型。
    /// 负责管理系统日志查询界面的业务逻辑，包括：
    /// 1. 多维度筛选（时间范围、关键字、业务分组、日志等级）。
    /// 2. 分页查询与后台静默加载（提升大数据量下的响应速度）。
    /// 3. 数据导出（支持 Excel/CSV）。
    /// 4. 动态图表支持（判定是否包含模拟量数据）。
    /// </summary>
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

        /// <summary>
        /// 搜索关键字。
        /// 支持模糊匹配日志内容、地址或状态值。
        /// </summary>
        public string Keyword
        {
            get => _keyword;
            set { _keyword = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 加载状态标识。
        /// 用于控制界面 loading 遮罩层的显示。
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        // 分类筛选
        public ObservableCollection<string> Categories { get; set; } = new ObservableCollection<string>();
        
        /// <summary>
        /// 当前选中的业务分组（如："1号门"、"值班室"）。
        /// </summary>
        private string _selectedCategory = "全部";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set { _selectedCategory = value; OnPropertyChanged(); }
        }

        // 记录等级筛选 (从数据库 LogType 加载)
        private List<string> _logLevelOptions = new List<string> { "全部" };
        public List<string> LogLevelOptions
        {
            get => _logLevelOptions;
            set { _logLevelOptions = value; OnPropertyChanged(); }
        }

        private string _selectedLogLevel = "全部";
        /// <summary>
        /// 当前选中的日志等级（如："报警记录"、"一般记录"）。
        /// 切换时会自动触发重新加载。
        /// </summary>
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
        /// <summary>
        /// 当前查看的日志表类型。
        /// "系统日志" 对应 PointLogs 表（开关量为主）。
        /// "模拟量数据" 对应 AnalogLogs 表（连续数值）。
        /// </summary>
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
        /// <summary>
        /// 系统中是否存在开启了日志记录的模拟量点位。
        /// 用于控制界面上是否显示“模拟量数据”切换选项。
        /// </summary>
        public bool HasAnalogPoints
        {
            get => _hasAnalogPoints;
            set { _hasAnalogPoints = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 当前页显示的日志数据集合。
        /// </summary>
        public ObservableCollection<PointLogEntity> Logs { get; set; } = new ObservableCollection<PointLogEntity>();

        // 分页属性
        private int _currentPage = 1;
        private int _totalPages = 1;
        private int _totalCount = 0;
        private int _pageSize = 1000;

        /// <summary>
        /// 当前页码 (1-based)。
        /// </summary>
        /// <summary>
        /// 当前页码 (1-based)。
        /// </summary>
        public int CurrentPage
        {
            get => _currentPage;
            set 
            {
                _currentPage = value; 
                OnPropertyChanged();
                JumpPage = value; // 同步更新跳转框
            }
        }

        private int _jumpPage = 1;
        /// <summary>
        /// 跳转页码输入框绑定的属性。
        /// </summary>
        public int JumpPage
        {
            get => _jumpPage;
            set { _jumpPage = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 总页数。
        /// </summary>
        public int TotalPages
        {
            get => _totalPages;
            set { _totalPages = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 查询结果总记录数。
        /// </summary>
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

        /// <summary>
        /// 完整的起始搜索时间（日期 + 时间）。
        /// 包含互锁逻辑：若起始时间晚于结束时间，会自动推迟结束时间。
        /// </summary>
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

        /// <summary>
        /// 完整的结束搜索时间（日期 + 时间）。
        /// 包含互锁逻辑：若结束时间早于起始时间，会自动提前起始时间。
        /// </summary>
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
        public ICommand LastPageCommand { get; set; } // 尾页
        public ICommand GoToPageCommand { get; set; } // 跳转
        public ICommand ResetCommand { get; set; }
        public ICommand ExportCommand { get; set; }

        /// <summary>
        /// 构造函数。
        /// 初始化命令绑定，加载分类筛选数据，并自动触发首次默认查询。
        /// </summary>
        public SystemLogViewModel()
        {
            SearchCommand = new RelayCommand(OnSearch);
            PrevPageCommand = new RelayCommand(OnPrevPage);
            NextPageCommand = new RelayCommand(OnNextPage);
            FirstPageCommand = new RelayCommand(OnFirstPage);
            LastPageCommand = new RelayCommand(OnLastPage);
            GoToPageCommand = new RelayCommand(OnGoToPage);
            ResetCommand = new RelayCommand(OnReset);
            ExportCommand = new RelayCommand(OnExport);

            // ExportCommand = new RelayCommand(OnExport); // Duplicate line removed

            // 加载分类
            LoadCategories();

            // 默认加载
            _ = LoadLogsAsync();
        }

        /// <summary>
        /// 执行查询操作。
        /// 重置到第一页并触发异步加载。
        /// </summary>
        private void OnSearch(object obj)
        {
            CurrentPage = 1;
            _ = LoadLogsAsync();
        }

        /// <summary>
        /// 翻到上一页。
        /// 仅更新显示层数据，不重新查询数据库（除非缓存未命中）。
        /// </summary>
        private void OnPrevPage(object obj)
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                _ = UpdateDisplayLogsAsync();
            }
        }

        /// <summary>
        /// 翻到下一页。
        /// </summary>
        /// <summary>
        /// 翻到下一页。
        /// </summary>
        private void OnNextPage(object obj)
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                _ = UpdateDisplayLogsAsync();
            }
        }

        /// <summary>
        /// 跳转到首页。
        /// </summary>
        private void OnFirstPage(object obj)
        {
            if (CurrentPage > 1)
            {
                CurrentPage = 1;
                _ = UpdateDisplayLogsAsync();
            }
        }

        /// <summary>
        /// 跳转到尾页。
        /// </summary>
        private void OnLastPage(object obj)
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage = TotalPages;
                _ = UpdateDisplayLogsAsync();
            }
        }

        /// <summary>
        /// 跳转到指定页。
        /// </summary>
        private void OnGoToPage(object obj)
        {
            if (JumpPage < 1) JumpPage = 1;
            if (JumpPage > TotalPages) JumpPage = TotalPages;
            
            if (JumpPage != CurrentPage)
            {
                CurrentPage = JumpPage;
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
        /// 辅助方法：构建 SQL 查询条件。
        /// 根据当前 UI 的所有筛选条件（时间、分类、类型、关键字）生成 FROM 和 WHERE 子句。
        /// 自动处理跨月分表查询（UNION ALL）。
        /// </summary>
        /// <returns>
        /// fromSource: 包含 UNION ALL 的多表查询源，或者单表名。
        /// baseWhere: 综合 WHERE 查询条件字符串。
        /// </returns>
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
            if (SelectedLogLevel != "全部")
            {
                // 反查 ID
                var logTypeId = PointLogEntity.LogTypeMap.FirstOrDefault(x => x.Value == SelectedLogLevel).Key;
                if (logTypeId > 0)
                {
                    baseWhere += $" AND LogType = {logTypeId}";
                }
            }

            return (fromSource, baseWhere);
        }

        /// <summary>
        /// 重置所有筛选条件到默认值（显示当前的全部系统日志）。
        /// </summary>
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

        /// <summary>
        /// 导出当前查询条件下的所有日志数据。
        /// 支持 Excel (*.xlsx) 和 CSV 格式。Excel 包含中文表头，适合报表；CSV 用于极大数据量备份。
        /// </summary>
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

        /// <summary>
        /// 异步加载日志列表（核心查询逻辑）。
        /// 流程：
        /// 1. 取消上次未完成的查询。
        /// 2. 构建跨月查询表名集合。
        /// 3. 查询总数 (COUNT) 并计算分页参数。
        /// 4. 优先加载第一页数据并刷新 UI。
        /// 5. 启动后台静默线程加载剩余数据到内存缓存。
        /// </summary>
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
        /// 后台静默批量加载。
        /// 在主线程显示第一页数据后，此方法会在后台分批次（每批5000条）拉取剩余数据到内存缓存。
        /// 目的：实现“秒开”体验，同时支持大数据的快速翻页。
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

        /// <summary>
        /// 初始化加载所有筛选选项。
        /// 包括：
        /// 1. 日志类型 (LogType 表)。
        /// 2. 动态扫描已有的业务分组 (DevicePointConfig 表中的 Category 字段)。
        /// 3. 检查是否有模拟量点位以决定是否显示“模拟量数据”选项。
        /// 4. 自动修补旧版日志数据库 Schema（后台任务）。
        /// </summary>
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
                    
                    // 1. 加载日志类型 (报警/状态/等)
                    var logTypes = db.FindAll<LogTypeEntity>().OrderBy(x => x.SortOrder).ToList();
                    var typeNames = new List<string> { "全部" };
                    typeNames.AddRange(logTypes.Select(x => x.Name));
                    
                    LogLevelOptions = typeNames;
                    OnPropertyChanged(nameof(LogLevelOptions));

                    // 更新全局缓存，用于显示列表中的类型名称
                    PointLogEntity.LogTypeMap.Clear();
                    foreach(var lt in logTypes) PointLogEntity.LogTypeMap[lt.Id] = lt.Name;

                    // 2. 加载业务分类
                    string sql = "SELECT DISTINCT Category FROM DevicePointConfig WHERE Category IS NOT NULL AND Category != ''";
                    var dt = db.ExecuteQuery(sql);
                    
                    foreach (System.Data.DataRow row in dt.Rows)
                    {
                        string cat = row["Category"].ToString();
                        if (!Categories.Contains(cat)) { Categories.Add(cat); }
                    }

                    // 3. 检查是否存在开启了记录功能的模拟量点位
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
                            
                            string[] prefixes = { "PointLogs", "AnalogLogs" };
                            foreach (var pfx in prefixes)
                            {
                                var tables = ldb.ExecuteQuery($"SHOW TABLES LIKE '{pfx}_%'");
                                foreach (System.Data.DataRow r in tables.Rows)
                                {
                                    string tName = r[0].ToString();
                                    try { ldb.ExecuteNonQuery($"ALTER TABLE `{tName}` ADD COLUMN Category VARCHAR(50);"); } catch { }
                                    try { ldb.ExecuteNonQuery($"ALTER TABLE `{tName}` ADD COLUMN UserName VARCHAR(50);"); } catch { }
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
