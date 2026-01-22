using Communicationlib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;


namespace DoorMonitorSystem.Assets.Commlib
{
    public static class ProtocolLoader
    {
        /// <summary>
        /// 扫描指定文件夹，加载所有实现 ICommBase 的协议插件
        /// </summary>
        public static Dictionary<string, ICommBase> LoadAllProtocols(string pluginsFolder)
        {
            var protocolDict = new Dictionary<string, ICommBase>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(pluginsFolder))
                throw new DirectoryNotFoundException($"插件目录不存在: {pluginsFolder}");

            var dllFiles = Directory.GetFiles(pluginsFolder, "*.dll");

            foreach (var dllPath in dllFiles)
            {
                try
                {
                    var asm = Assembly.LoadFrom(dllPath);
                    var types = asm.GetTypes().Where(t =>
                        typeof(ICommBase).IsAssignableFrom(t) &&
                        !t.IsInterface && !t.IsAbstract);

                    foreach (var type in types)
                    {
                        if (Activator.CreateInstance(type) is ICommBase instance)
                        {
                            if (!protocolDict.ContainsKey(instance.ProtocolKey))
                            {
                                protocolDict.Add(instance.ProtocolKey, instance);
                                Debug.WriteLine($"已加载协议插件: {instance.ProtocolKey} 来自 {dllPath}");
                            }
                            else
                            {
                                Debug.WriteLine($"重复的 ProtocolKey：{instance.ProtocolKey}，已忽略 {type.FullName}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"加载插件失败: {dllPath}, 错误: {ex.Message}");
                }
            }

            return protocolDict;
        }
    }
}


