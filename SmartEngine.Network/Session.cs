using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace SmartEngine.Network
{
    /// <summary>
    /// 会话类，每个已经建立的连接都将拥有一个自己的会话实例
    /// </summary>
    /// <typeparam name="T">封包Opcode枚举</typeparam>
    public class Session<T>
    {
        internal Network<T> netIO;
        internal ClientManager<T> clientManager;

        /// <summary>
        /// 该连接是否已经连接上
        /// </summary>
        public bool Connected
        {
            get;
            set;
        }
        /// <summary>
        /// 该Session所属ClientManager
        /// </summary>
        public ClientManager<T> ClientManager { get { return clientManager; } }

        /// <summary>
        /// 该Session的网络层
        /// </summary>
        public Network<T> Network { get { return netIO; } }

        public virtual void OnConnect()
        {

        }

        public virtual void OnDisconnect() { }

        public ST As<ST>() where ST : Session<T>
        {
            return (ST)this;
        }
    }
}
