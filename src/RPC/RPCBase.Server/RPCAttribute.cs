using System;

namespace RPCBase
{
    public enum ServiceScope
    {
        InterServer,
        ClientToServer,
        ServerToClient,
    }

    // publish notify service
    public class NotifyAttribute : ServiceAttribute
    {
        public NotifyAttribute() : base(ServiceScope.InterServer)
        {
            
        }
    }

    // point-to-point RPC
    public class ServiceAttribute : Attribute
    {
        public ServiceScope ServiceScope { get; private set; }
        public ServiceAttribute(ServiceScope scope)
        {
            ServiceScope = scope;
        }
    }

    // point-to-point RPC, specially for server->client
    public class SyncAttribute : ServiceAttribute
    {
        public bool Multicast { get; private set; }
        public SyncAttribute(bool multicast = false): base(ServiceScope.ServerToClient)
        {
            Multicast = multicast;
        }
    }

    public class DivisionalAttribute : Attribute
    {
        public bool Divisional { get; private set; }
        public DivisionalAttribute(bool divisional)
        {
            Divisional = true;
        }
    }
}
