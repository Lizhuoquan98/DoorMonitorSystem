using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using DoorMonitorSystem.Models.system;
using System.Collections.Generic;

namespace DoorMonitorSystem.Views
{
    public partial class LoginWindow : Window
    {
        public UserEntity LoggedInUser { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            LoadUsers();
            
            // Auto focus
            Loaded += (s, e) => ComboUser.Focus();
            KeyDown += LoginWindow_KeyDown;
        }

        private void LoadUsers()
        {
            try
            {
                if (GlobalData.SysCfg == null) return;
                
                using var db = new Assets.Helper.SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();
                if (db.IsConnected)
                {
                    // Verify table exists first (if needed, but usually safe)
                    var users = db.Query<UserEntity>("Sys_Users", ""); 
                    if (users != null)
                    {
                        ComboUser.ItemsSource = users;
                        // Select Admin or first user by default if available
                        if (users.Count > 0)
                            ComboUser.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                LblError.Text = "加载用户列表失败: " + ex.Message;
            }
        }

        private void LoginWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnLogin_Click(null, null);
            }
            else if (e.Key == Key.Escape)
            {
                BtnCancel_Click(null, null);
            }
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = ComboUser.Text;
            string password = TxtPassword.Password;

            if (string.IsNullOrWhiteSpace(username))
            {
                LblError.Text = "请输入用户名";
                return;
            }

            try
            {
                using var db = new Assets.Helper.SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();
                if (db.IsConnected)
                {
                    // Simple query
                    // Security Note: Use parameterized query in production to prevent SQL injection
                    string sql = $"Username='{username}' AND Password='{password}'"; 
                    // Better:
                    var users = db.Query<UserEntity>("Sys_Users", "Username=@u AND Password=@p", 
                        new MySql.Data.MySqlClient.MySqlParameter("@u", username),
                        new MySql.Data.MySqlClient.MySqlParameter("@p", password));

                    if (users != null && users.Count > 0)
                    {
                        var user = users[0];
                        if (!user.IsEnabled)
                        {
                            LblError.Text = "该账户已被禁用";
                            Assets.Services.DataManager.Instance.LogOperation("LoginFailed", $"账户被禁用: {username}", "Failed");
                            return;
                        }

                        // Success
                        LoggedInUser = user;
                        Assets.Services.DataManager.Instance.LogOperation("UserLogin", $"用户登录成功: {user.RealName} ({user.Role})");
                        
                        // Update last login
                        user.LastLoginTime = DateTime.Now;
                        db.Update(user); // Assuming Update works

                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        LblError.Text = "用户名或密码错误";
                         Assets.Services.DataManager.Instance.LogOperation("LoginFailed", $"密码错误或用户不存在/尝试用户: {username}", "Failed");
                    }
                }
                else
                {
                    LblError.Text = "连接数据库失败";
                }
            }
            catch (Exception ex)
            {
                LblError.Text = "登录异常: " + ex.Message;
                Assets.Services.DataManager.Instance.LogOperation("LoginException", $"登录过程异常: {ex.Message}", "Error");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
