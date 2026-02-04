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
                    // 安全策略：Admin 账号不显示在下拉列表中，防止被猜解
                    var allUsers = db.Query<UserEntity>("Sys_Users", "");
                    if (allUsers != null)
                    {
                        var visibleUsers = allUsers.Where(u => !u.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase)).ToList();
                        ComboUser.ItemsSource = visibleUsers;

                        // Select first available user by default if available
                        if (visibleUsers.Count > 0)
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
                    // 1. Find user by username only
                    var users = db.Query<UserEntity>("Sys_Users", "Username=@u",
                        new MySql.Data.MySqlClient.MySqlParameter("@u", username));

                    if (users != null && users.Count > 0)
                    {
                        var user = users[0];
                        bool isPasswordValid = false;

                        // 验证逻辑：同时支持 MD5 和 明文 (平滑过渡)
                        string inputMd5 = Assets.Helper.CryptoHelper.ComputeMD5(password);

                        if (user.Password == inputMd5)
                        {
                            isPasswordValid = true;
                        }
                        else if (user.Password == password)
                        {
                            // 兼容旧的明文密码，并在登录成功后自动升级为 MD5
                            isPasswordValid = true;
                            user.Password = inputMd5;
                        }

                        if (isPasswordValid)
                        {
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
