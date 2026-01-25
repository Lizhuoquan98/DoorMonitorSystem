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

namespace DoorMonitorSystem.ViewModels
{
    public class SystemLogViewModel : NotifyPropertyChanged
    {
        private DateTime _startDate = DateTime.Now.AddDays(-1);
        private DateTime _endDate = DateTime.Now;
        private string _keyword = "";
        private bool _isLoading;

        public DateTime StartDate
        {
            get => _startDate;
            set { _startDate = value; OnPropertyChanged(); }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set { _endDate = value; OnPropertyChanged(); }
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

        public ObservableCollection<PointLogEntity> Logs { get; set; } = new ObservableCollection<PointLogEntity>();

        // 分页属性
        private int _currentPage = 1;
        private int _totalPages = 1;
        private int _totalCount = 0;
        private int _pageSize = 50;

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
        private string _searchEndTime = "23:59:59";

        public string SearchStartTime
        {
            get => _searchStartTime;
            set { _searchStartTime = value; OnPropertyChanged(); }
        }

        public string SearchEndTime
        {
            get => _searchEndTime;
            set { _searchEndTime = value; OnPropertyChanged(); }
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
                _ = LoadLogsAsync();
            }
        }

        private void OnNextPage(object obj)
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                _ = LoadLogsAsync();
            }
        }

        private void OnReset(object obj)
        {
            StartDate = DateTime.Now.AddDays(-1);
            EndDate = DateTime.Now;
            SearchStartTime = "00:00:00";
            SearchEndTime = "23:59:59";
            Keyword = "";
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
            saveFileDialog.FileName = $"系统日志_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            
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
                        while (d <= EndDate)
                        {
                            string tableName = $"PointLogs_{d:yyyyMM}";
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
                            baseWhere += $" AND (Message LIKE '%{Keyword}%' OR Address LIKE '%{Keyword}%')";
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
                                时间 = x.LogTime.ToString("yyyy-MM-dd HH:mm:ss"),
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
                                    sw.WriteLine($"{log.LogTime:yyyy-MM-dd HH:mm:ss},{log.LogTypeDisplay},{log.DeviceID},{EscapeCsv(log.Address ?? "")},{log.ValDisplay},{EscapeCsv(log.Message ?? "")}");
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

            // 检查查询范围（禁止超过7天）
            if ((EndDate.Date - StartDate.Date).TotalDays > 7)
            {
                System.Windows.MessageBox.Show("单次查询跨度不能超过 7 天，请缩小时间范围。", "查询限制", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            IsLoading = true;
            Logs.Clear();

            try
            {
                if (GlobalData.SysCfg != null)
                {
                    await Task.Run(() =>
                    {
                        string logDb = GlobalData.SysCfg.LogDatabaseName;
                        using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, logDb);
                        db.Connect();

                        // 组合完整时间串 (用于 SQL Comparison)
                        string startDt = $"{StartDate:yyyy-MM-dd} {SearchStartTime}";
                        string endDt = $"{EndDate:yyyy-MM-dd} {SearchEndTime}";

                        // 2. 根据起止时间判定涉及的分表 (1周最多跨2个月)
                        List<string> tableList = new();
                        DateTime d = new DateTime(StartDate.Year, StartDate.Month, 1);
                        while (d <= EndDate)
                        {
                            string tableName = $"PointLogs_{d:yyyyMM}";
                            if (db.TableExists(tableName)) tableList.Add(tableName);
                            d = d.AddMonths(1);
                        }

                        if (tableList.Count == 0)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() => { TotalCount = 0; TotalPages = 1; });
                            return;
                        }

                        // 3. 构造数据源 (单一表或 UNION ALL)
                        string fromSource = tableList.Count == 1 
                            ? $"`{tableList[0]}`" 
                            : $"(" + string.Join(" UNION ALL ", tableList.Select(t => $"SELECT * FROM `{t}`")) + ") AS CombinedLogs";

                        // 4. 构建基础 Where 子句
                        string baseWhere = $"LogTime BETWEEN '{startDt}' AND '{endDt}'";
                        if (!string.IsNullOrWhiteSpace(Keyword))
                        {
                            baseWhere += $" AND (Message LIKE '%{Keyword}%' OR Address LIKE '%{Keyword}%')";
                        }

                        // 5. 查询总条数
                        string countSql = $"SELECT COUNT(*) FROM {fromSource} WHERE {baseWhere}";
                        object countResult = db.ExecuteScalar(countSql);
                        int total = 0;
                        if (countResult != null) int.TryParse(countResult.ToString(), out total);

                        // 6. 计算分页
                        int pages = (int)Math.Ceiling((double)total / _pageSize);
                        if (pages < 1) pages = 1;
                        int offset = (CurrentPage - 1) * _pageSize;

                        // 更新UI属性
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            TotalCount = total;
                            TotalPages = pages;
                            if (CurrentPage > TotalPages) CurrentPage = 1; 
                        });

                        // 7. 查询当前页数据
                        string finalSql = $"SELECT * FROM {fromSource} WHERE {baseWhere} ORDER BY LogTime DESC LIMIT {offset}, {_pageSize}";
                        var result = db.Query<PointLogEntity>(finalSql); 
                        
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            foreach (var log in result) Logs.Add(log);
                        });
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
    }
}
