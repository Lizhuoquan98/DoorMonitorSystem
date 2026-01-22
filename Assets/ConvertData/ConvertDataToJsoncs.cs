 using Newtonsoft.Json;
using System; 
using System.IO; 
using System.Windows;

namespace DoorMonitorSystem.Assets.ConvertData
{
    public  class ConvertDataToJsoncs
    {
        /// <summary>
        /// 返回Json字符串
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private static string ConvertDataToJson(object obj)
        {
            return JsonConvert.SerializeObject(obj, (Newtonsoft.Json.Formatting)Formatting.Indented);
        }

        /// <summary>
        ///  保存数据到Json文件
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static bool SaveDataToJson(object obj, string filePath)
        {
            try
            {
                File.WriteAllText(filePath, ConvertDataToJson(obj));
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "保存出错");               
            }
            return false;
        }

        /// <summary>
        /// 加载Json文件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static T? LoadDataFromJson<T>(string filePath)
        {
            if (File.Exists(filePath))
            {
                string jsonData = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<T>(jsonData);
            }
            return default;
        }

    }
}
