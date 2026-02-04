using System;
using System.Security.Cryptography;
using System.Text;

namespace DoorMonitorSystem.Assets.Helper
{
    public static class CryptoHelper
    {
        public static string ComputeMD5(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }
    }
}
