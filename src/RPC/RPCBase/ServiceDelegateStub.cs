using System;
using System.Collections.Generic;
using System.IO;

namespace RPCBase
{
    // RpcDelegate
    public sealed class ServiceDelegateStub :IMessageConsumer
    {
        public IDataSender DataSender { get { return dataSender; } }
        public IMethodSerializer MethodSerializer { get { return methodSerializer; } }
        public RoutingRule RoutingRule { get { return routingRule; } }

        private readonly IDataSender dataSender;
        private readonly IMethodSerializer methodSerializer;
        private readonly Dictionary<uint, InvokeOperation> invokeOperations = new Dictionary<uint, InvokeOperation>();
        private uint currentInvokeId;
        private readonly RoutingRule routingRule;
        public ServiceDelegateStub(IDataSender dataSender, IMethodSerializer methodSerializer, RoutingRule routingRule)
        {
            this.dataSender = dataSender;
            this.methodSerializer = methodSerializer;
            this.routingRule = routingRule;
        }

        public void OnReceiveMessage(Mode mode, byte[] buf, int offset, MessageConsumedCallback callback)
        {
            var stream = new MemoryStream(buf);
            var br = new BinaryReader(stream);

            var invokeId = br.ReadUInt32();
            InvokeOperation handle;
            lock (invokeOperations)
            {
                invokeOperations.TryGetValue(invokeId, out handle);
                invokeOperations.Remove(invokeId);
            }

            if (handle != null)
            {
                if (br.ReadInt32() > 0)
                {
                    handle.SetResult(methodSerializer.ReadReturn(handle.MethodId, br));
                }
                else
                {
                    // todo exception type
                    handle.SetException(new InvokeOperationException(new Exception("br.ReadInt32() <= 0")));
                }
            }
        }

        public void Notify(uint methodId, byte[] forwardKey, params object[] args)
        {
            DoSend(0, methodId, forwardKey, args);
        }

        public InvokeOperation Invoke(uint methodId, byte[] forwardKey, params object[] args)
        {
            var handle = new InvokeOperation();

            Invoke(handle, methodId, forwardKey, args);

            return handle;
        }

        public InvokeOperation<TResult> Invoke<TResult>(uint methodId, byte[] forwardKey, params object[] args)
        {
            var handle = new InvokeOperation<TResult>();
            Invoke(handle, methodId, forwardKey, args);

            return handle;
        }

        private void Invoke(InvokeOperation op, uint methodId, byte[] forwardKey, object[] args)
        {
            op.MethodId = methodId;
            lock (invokeOperations)
            {
                op.InvokeId = currentInvokeId++;
                invokeOperations[op.InvokeId] = op;
            }

            DoSend(op.InvokeId, methodId, forwardKey, args);
        }

        private void DoSend(uint invokeId, uint methodId, byte[] forwardKey, object[] args)
        {
            // todo to avoid MemoryStream and BinaryWriter allocations every time data comes
            // todo RPCBase.Server could change to use ThreadLocal<T>
            // todo RPCBase.Clent could use singleton directly
            var buffer = new MemoryStream(32);
            var bufferWriter = new BinaryWriter(buffer);

            var uuidBytes = dataSender.Uuid.ToByteArray();

            bufferWriter.Write(uuidBytes.Length);
            bufferWriter.Write(uuidBytes);
            bufferWriter.Write(invokeId);
            methodSerializer.Write(methodId, args, bufferWriter);

            dataSender.Send(buffer.GetBuffer(), forwardKey, routingRule);
            //dataSender.Send(buffer.GetBuffer(), serviceMetaInfo.GetDelegateRoutingKey(forwardKey), serviceMetaInfo.GetDelegateExchangeName());
        }
    }
}
