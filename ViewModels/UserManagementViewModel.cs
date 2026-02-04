using Base;
using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Base;
using DoorMonitorSystem.Models.system;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DoorMonitorSystem.ViewModels
{
    public class UserManagementViewModel : NotifyPropertyChanged
    {
        private ObservableCollection<UserEntity> _users = new();
        private UserEntity? _selectedUser;
        private bool _isEditing;
        private string _editTitle = "用户详情";
        
        // Form Fields
        private int _formId;
        private string _formUsername = "";
        private string _formRealName = "";
        private string _formRole = "User";
        private bool _formIsEnabled = true;
        // Password is handled separately or via PasswordBox binding helper, for simplicity we use plain property here
        private string _formPassword = ""; 

        #region Properties

        public ObservableCollection<UserEntity> Users
        {
            get => _users;
            set { _users = value; OnPropertyChanged(); }
        }

        public UserEntity? SelectedUser
        {
            get => _selectedUser;
            set
            {
                _selectedUser = value;
                OnPropertyChanged();
                if (_selectedUser != null)
                {
                    LoadForm(_selectedUser);
                }
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set 
            { 
                _isEditing = value; 
                OnPropertyChanged(); 
            }
        }

        public string EditTitle
        {
            get => _editTitle;
            set { _editTitle = value; OnPropertyChanged(); }
        }

        public string FormUsername
        {
            get => _formUsername;
            set { _formUsername = value; OnPropertyChanged(); }
        }

        public string FormRealName
        {
            get => _formRealName;
            set { _formRealName = value; OnPropertyChanged(); }
        }

        public string FormRole
        {
            get => _formRole;
            set 
            { 
                _formRole = value; 
                OnPropertyChanged(); 
                UpdatePermissions(_formRole);
            }
        }

        public bool FormIsEnabled
        {
            get => _formIsEnabled;
            set { _formIsEnabled = value; OnPropertyChanged(); }
        }

        public string FormPassword
        {
            get => _formPassword;
            set { _formPassword = value; OnPropertyChanged(); }
        }

        // Permission Display Properties
        private bool _permSystemConfig;
        public bool PermSystemConfig { get => _permSystemConfig; set { _permSystemConfig = value; OnPropertyChanged(); } }

        private bool _permUserMgmt;
        public bool PermUserMgmt { get => _permUserMgmt; set { _permUserMgmt = value; OnPropertyChanged(); } }

        private bool _permDoorControl;
        public bool PermDoorControl { get => _permDoorControl; set { _permDoorControl = value; OnPropertyChanged(); } }

        private bool _permLogViewing;
        public bool PermLogViewing { get => _permLogViewing; set { _permLogViewing = value; OnPropertyChanged(); } }

        private void UpdatePermissions(string role)
        {
            // Default Reset
            PermSystemConfig = false;
            PermUserMgmt = false;
            PermDoorControl = false;
            PermLogViewing = false;

            if (string.IsNullOrEmpty(role)) return;

            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                PermSystemConfig = true;
                PermUserMgmt = true;
                PermDoorControl = true;
                PermLogViewing = true;
            }
            else if (role.Equals("Engineer", StringComparison.OrdinalIgnoreCase))
            {
                PermSystemConfig = false; // Engineer usually handles Parameters, not SysCfg
                PermUserMgmt = true;      // We just enabled this
                PermDoorControl = true;
                PermLogViewing = true;
            }
            else if (role.Equals("Operator", StringComparison.OrdinalIgnoreCase))
            {
                PermDoorControl = true;
                PermLogViewing = true;
            }
            else if (role.Equals("Observer", StringComparison.OrdinalIgnoreCase))
            {
                PermLogViewing = true;
                // Observer is Read-Only
            }
        }

        // Roles Selection
        public ObservableCollection<string> Roles { get; } = new ObservableCollection<string> { "Admin", "Engineer", "Operator", "Observer" };

        #endregion

        #region Commands

        public ICommand CreateCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        public UserManagementViewModel()
        {
            CreateCommand = new RelayCommand(OnCreate);
            SaveCommand = new RelayCommand(OnSave);
            DeleteCommand = new RelayCommand(OnDelete);
            CancelCommand = new RelayCommand(OnCancel);

            LoadData();
        }

        private void LoadData()
        {
            Task.Run(() =>
            {
                try
                {
                    if (GlobalData.SysCfg == null) return;
                     using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    db.Connect();
                    var list = db.FindAll<UserEntity>(); // Or Query<UserEntity>("Sys_Users", "1=1");
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Users = new ObservableCollection<UserEntity>(list);
                    });
                }
                catch (Exception ex)
                {
                    LogHelper.Error("[UserVM] Load Failed", ex);
                }
            });
        }

        private void LoadForm(UserEntity user)
        {
            _formId = user.Id;
            FormUsername = user.Username;
            FormRealName = user.RealName ?? "";
            FormRole = user.Role ?? "User";
            FormIsEnabled = user.IsEnabled;
            FormPassword = ""; // 不回显密码，留空表示不修改
            IsEditing = true;
            EditTitle = $"编辑用户: {user.Username}";
        }

        private void OnCreate(object obj)
        {
            SelectedUser = null; // Clear selection
            _formId = 0;
            FormUsername = "";
            FormRealName = "";
            FormRole = "Operator";
            FormIsEnabled = true;
            FormPassword = "";
            IsEditing = true;
            EditTitle = "新建用户";
        }

        private void OnSave(object obj)
        {
            if (string.IsNullOrWhiteSpace(FormUsername))
            {
                MessageBox.Show("用户名不能为空");
                return;
            }

            // 新增用户时密码必填
            if (_formId == 0 && string.IsNullOrWhiteSpace(FormPassword))
            {
                MessageBox.Show("新建用户必须设置初始密码");
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    
                    if (GlobalData.SysCfg == null) return;
                    using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    db.Connect();

                    var user = new UserEntity
                    {
                        Id = _formId,
                        Username = FormUsername,
                        RealName = FormRealName,
                        Role = FormRole,
                        IsEnabled = FormIsEnabled,
                        LastLoginTime = DateTime.Now 
                    };

                    if (_formId == 0)
                    {
                        // Check duplicate
                        var exist = db.Query<UserEntity>("Sys_Users", "Username = @u", new MySql.Data.MySqlClient.MySqlParameter("@u", FormUsername));
                        if (exist.Count > 0)
                        {
                            MessageBox.Show("用户名已存在");
                            return;
                        }
                        
                        user.Password = CryptoHelper.ComputeMD5(FormPassword); // 新用户加密密码
                        user.CreateTime = DateTime.Now;
                        db.Insert(user);
                        LogHelper.Info($"[用户管理] 管理员新增了用户账号: {user.Username} (姓名: {user.RealName}, 角色: {user.Role})");
                    }
                    else
                    {
                        var original = db.SelectById<UserEntity>(_formId);
                        if(original != null)
                        {
                             user.CreateTime = original.CreateTime; // Keep original create time
                             
                             // 密码处理逻辑：如果表单密码为空，则保持原密码；否则更新为新加密密码
                             if (string.IsNullOrWhiteSpace(FormPassword))
                             {
                                 user.Password = original.Password;
                             }
                             else
                             {
                                 user.Password = CryptoHelper.ComputeMD5(FormPassword);
                             }
                        }
                        db.Update(user);
                        
                        string pwLog = string.IsNullOrWhiteSpace(FormPassword) ? "" : "(密码已修改)";
                        LogHelper.Info($"[用户管理] 管理员更新了用户账号信息: {user.Username} {pwLog} (角色: {user.Role}, 状态: {(user.IsEnabled ? "启用" : "禁用")})");
                    }

                    LoadData();
                    MessageBox.Show("保存成功");
                }
                catch (Exception ex)
                {
                    LogHelper.Error("[UserVM] Save Failed", ex);
                    MessageBox.Show("保存失败: " + ex.Message);
                }
            });
        }

        private void OnDelete(object obj)
        {
            if (_formId == 0) return;
            
            if (MessageBox.Show($"确定要删除用户 {FormUsername} 吗?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            Task.Run(() =>
            {
                try
                {
                    if (GlobalData.SysCfg == null) return;
                    using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    db.Connect();
                    
                    var username = FormUsername;
                    var user = new UserEntity { Id = _formId };
                    db.Delete(user);
                    LogHelper.Warn($"[用户管理] 管理员删除了用户账号: {username}");
                    
                    LoadData();
                    IsEditing = false; // Close form logic
                    _formId = 0;
                }
                catch (Exception ex)
                {
                    LogHelper.Error($"[用户管理] 删除用户失败: {ex.Message}", ex);
                    MessageBox.Show("删除失败");
                }
            });
        }

        private void OnCancel(object obj)
        {
            if (SelectedUser != null)
            {
                LoadForm(SelectedUser);
            }
            else
            {
                IsEditing = false;
                _formId = 0;
            }
        }
    }
}
