using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Network;
using Phial;

namespace RPCBase.Server
{
    // this layer is responsible for reconnecting
    public class GateAdaptor : IAdaptor, IMulticastSender, IPollable
    {
        // identify current adaptor
        private readonly Guid uuid = Guid.NewGuid();

        public Guid Uuid
        {
            get { return uuid; }
        }

        private ClientNetwork handle;

        // hold impls and delegates registered,
        // when a msg arrived,
        // dispatch msg to specific impl or delegate
        private readonly Dictionary<int, IMessageConsumer> implements = new Dictionary<int, IMessageConsumer>();
        private readonly Dictionary<int, IMessageConsumer> delegates = new Dictionary<int, IMessageConsumer>();

        public GateAdaptor(string connIp, int port)
        {
            handle = new ClientNetwork(connIp, port)
            {
                ConnectorConnected = OnHandleConnected,
                ConnectorDisconnected = OnHandleDisconnected,
                ConnectorMessageReceived = OnHandleMessageReceived,
            };
        }

        public void RegisterDelegate(IMessageConsumer consumer, string serviceId)
        {
            var rule = MetaData.GetServiceRoutingRule(serviceId);
            if (rule == null)
            {
                throw new Exception();
            }

            delegates[rule.GateRule.GetServiceId()] = consumer;
        }

        public void RegisterImpl(IMessageConsumer impl, string serviceId)
        {
            var rule = MetaData.GetServiceRoutingRule(serviceId);
            if (rule == null)
            {
                throw new Exception();
            }

            implements[rule.GateRule.GetServiceId()] = impl;
        }

        public void Send(byte[] buffer, byte[] dstUuid, RoutingRule routingRule)
        {
            var serviceId = routingRule.GateRule.GetServiceId();
            var head = new byte[sizeof(Int32)+1];

            Helper.Int32ToByteArray(serviceId, head, 0);
            head[4] = (byte) Mode.Sync;

            handle.SendDatav(MessageTypeToBuffer(GateMessage.Unicast), dstUuid, head, buffer);
        }

        public void MulticastSend(byte[] buffer, int forwardId, RoutingRule routingRule)
        {
            var head = new byte[2*sizeof(Int32)+1];

            Helper.Int32ToByteArray(forwardId, head, 0);
            Helper.Int32ToByteArray(routingRule.GateRule.GetServiceId(), head, 4);
            head[8] = (byte)Mode.Sync;

            handle.SendDatav(MessageTypeToBuffer(GateMessage.Multicast), head, buffer);
        }

        public void Subscribe<T>(byte[] dstUuid)
        {
            var routingRule = MetaData.GetServiceRoutingRule(typeof(T));
            var head = new byte[sizeof(Int32)];
            var serviceId = routingRule.GateRule.GetServiceId();

            Helper.Int32ToByteArray(serviceId, head, 0);

            handle.SendDatav(MessageTypeToBuffer(GateMessage.Subscribe), head, dstUuid);
        }

        public void AddForward<T>(byte[] dstUuid, int group)
        {
            var routingRule = MetaData.GetServiceRoutingRule(typeof(T));

            GateForwardControlMessage(GateMessage.AddForward, dstUuid, group, routingRule);
        }
        public void RemoveForward<T>(byte[] dstUuid, int group)
        {
            var routingRule = MetaData.GetServiceRoutingRule(typeof(T));

            GateForwardControlMessage(GateMessage.RemoveForward, dstUuid, group, routingRule);
        }

        public void BeginReceive()
        {
            ThreadPool.QueueUserWorkItem((o) =>
            {
                Log.Info("BeginReceive handle.Connect(true);");
                try
                {
                    handle.Connect();
                }
                catch (Exception e)
                {
                    // todo event that connect failed must be thrown to upper layer
                    Log.Error("MqttAdaptor BeginReceive throw exp:{0}", e);
                    throw;
                }
            });
        }

        public void Poll()
        {
            handle.Poll();
        }

        private static byte[] MessageTypeToBuffer(GateMessage message)
        {
            var buffer = new [] { (byte)message };

            return buffer;
        }

        private void GateForwardControlMessage(GateMessage message, byte[] dstUuid, int group, RoutingRule routingRule)
        {
            var head = new byte[sizeof(Int32)];

            Helper.Int32ToByteArray(group, head, 0);

            handle.SendDatav(MessageTypeToBuffer(message), dstUuid, head);
        }

        // not: all of the handlers would be notified
        // only when user polled
        // thus, ensure thread-safety

        private void OnHandleConnected(ILocal remote, Exception e)
        {
            handle.SendDatav(MessageTypeToBuffer(GateMessage.Handshake), uuid.ToByteArray());
        }

        private void OnHandleDisconnected()
        {

        }

        private void OnHandleMessageReceived(Message msg)
        {
            var stream = new MemoryStream(msg.Buffer);
            var br = new BinaryReader(stream);

            var mode = (GateMessage)br.ReadByte();

            if (mode == GateMessage.Received)
            {
                var serviceId = br.ReadInt32();
                var rpcMode = MetaData.GetMode(br.ReadByte());
                var toQuery = rpcMode == Mode.Return ? delegates : implements;
                IMessageConsumer msgConsumer;

                if (toQuery.TryGetValue(serviceId, out msgConsumer))
                {
                    // todo eliminate magic number
                    msgConsumer.OnReceiveMessage(rpcMode, msg.Buffer, 1 + 4 + 1, null);
                }
            }
        }

        internal enum GateMessage
        {
            Handshake = 1,
            Received = 2,
            Subscribe = 3,
            AddForward = 7,
            RemoveForward = 22,
            Unicast = 33,
            Multicast = 36,
        }

    }
}
