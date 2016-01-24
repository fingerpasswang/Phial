using System;
using System.Collections.Generic;
using System.Text;

namespace Ox.RPCBase
{
    public class GateAdaptor : IAdaptor
    {
        public Guid Uuid { get; }
        public void RegisterDelegate(IMessageConsumer consumer, string serviceId)
        {
            throw new NotImplementedException();
        }

        public void Send(byte[] buffer, byte[] dstUuid, RoutingRule routingRule)
        {
            throw new NotImplementedException();
        }

        public void RegisterImpl(IMessageConsumer consumer, string serviceId)
        {
            throw new NotImplementedException();
        }

        public void BeginReceive()
        {
            throw new NotImplementedException();
        }
    }

    public enum GateMessage
    {
        Forward,
        Add,
        Remove,
    }
}
