
using System;

namespace RPCBase
{
    public enum Mode
    {
        Invoke,
        Return,
        Notify,
        Sync,
    }

    public delegate void MessageConsumedCallback(byte[] buffer, byte[] dstUuid, RoutingRule routingRule);

    public interface IMessageConsumer
    {
        void OnReceiveMessage(Mode mode, byte[] buf, int offset, MessageConsumedCallback callback);
    }

    public interface IDataSender
    {
        Guid Uuid { get; }
        void RegisterDelegate(IMessageConsumer consumer, string serviceId);

        // todo it's a necessity that a extra arg indicates buffer's indeed length
        // sometimes we push a overlen buffer, only the first of section of which would be sent
        void Send(byte[] buffer, byte[] dstUuid, RoutingRule routingRule);
    }

    public interface IMulticastSender
    {
        void MulticastSend(byte[] buffer, int forwardId, RoutingRule routingRule);
    }

    public interface IDataReceiver
    {
        void RegisterImpl(IMessageConsumer consumer, string serviceId);
        void BeginReceive();
    }

    public interface IAdaptor : IDataSender, IDataReceiver
    {
    }

    public interface IPollable
    {
        void Poll();
    }
}
