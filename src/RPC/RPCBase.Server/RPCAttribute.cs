using System;

namespace RPCBase
{
    // publish notify service
    public class NotifyAttribute : ServiceAttribute
    {
       
    }

    // point-to-point RPC
    public class ServiceAttribute : Attribute
    {
        public bool Divisional { get; private set; }
        public ServiceAttribute( bool divisional = true)
        {
            Divisional = divisional;
        }
    }

    // point-to-point RPC, specially for server->client
    public class SyncAttribute : ServiceAttribute
    {
        public bool Multicast { get; private set; }
        public SyncAttribute(bool multicast = false)
        {
            Multicast = multicast;
        }
    }
}
