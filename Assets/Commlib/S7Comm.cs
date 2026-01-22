
using Communication.Protocol.Abstract;
using Communicationlib;
using Communicationlib.config; 
using Sharp7;  // 需引用Sharp7.dll或对应命名空间
using System;
using System.Collections.Generic;

namespace DoorMonitorSystem.Assets.Commlib
{
    public class S7Comm : ICommBase, IDisposable
    {
        private S7Client client = new();

        private string ip = "192.168.0.1";
        private int rack = 0;
        private int slot = 2;

        public string ProtocolKey => "S7_TCP";

        public bool IsConnected => client.Connected;



        public void Initialize(List<CommParamEntity> parameters)
        {
            foreach (var p in parameters)
            {
                if (p.Name == "IP") ip = p.Value;
                else if (p.Name == "Rack" && int.TryParse(p.Value, out int r)) rack = r;
                else if (p.Name == "Slot" && int.TryParse(p.Value, out int s)) slot = s;
            }
        }

        public void Open()
        {
            int res = client.ConnectTo(ip, rack, slot);
            if (res != 0)
                throw new Exception($"连接PLC失败，错误码：{res}");
        }

        public void Close()
        {
            client.Disconnect();
        }


        private readonly object _lockObj = new(); // 放在类成员中
        /// <summary>
        /// 读取DB块数据
        /// </summary> 
        public byte[] SendAndReceive<T>(T frame) 
        {
            if (!IsConnected)
                throw new InvalidOperationException("PLC未连接");
            if (frame is not ushort[])
                return [];  
            ushort[]? request = frame as ushort[]; 
            if (request.Length < 3)
                throw new ArgumentException("请求格式错误"); 
            int dbNumber = request[0];
            int start = request[1];
            int length = request[2];

            byte[] buffer = new byte[length];
            int result;
            lock (_lockObj)
            {
                try
                {
                    result = client.DBRead(dbNumber, start, length, buffer);
                    return buffer;
                }
                catch (Exception)
                { throw; }
                 
            }
        }
        public virtual Dictionary<string, ProtocolMethodBase> GetSupportedMethods()
        {
            return new Dictionary<string, ProtocolMethodBase>
            {
                {
                    "PollStatus",
                    new ProtocolAction
                    {
                        Name = "PollStatus",
                        Description = "定时轮询状态",
                        Mode = ExecutionMode.Polling,
                        IntervalMs = 1000,
                        Action =  () =>
                        { SendAndReceive<ushort[]>([300,0,776]); return System.Threading.Tasks.Task.CompletedTask; }
                    }
                }
            };
        }

        public void Dispose()
        {
            Close();
            client = null!;
        }

    }
}