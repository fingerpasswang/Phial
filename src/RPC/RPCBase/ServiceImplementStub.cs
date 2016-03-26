using System;
using System.IO;
using System.Threading;

namespace RPCBase
{
    public interface IServiceMethodDispatcher
    {
        void Dispatch(IRpcImplInstnce impl, RpcMethod method, ServiceImplementStub.SendResult cont);
    }

    // SetSourceUuid used to construct context for one session, user can customize how SetSourceUuid performs
    public interface IRpcImplInstnce
    {
        IRpcImplInstnce SetSourceUuid(byte[] srcUuid);
    }

    // RpcImpl
    public sealed class ServiceImplementStub : IMessageConsumer
    {
        public delegate void SendResult(object value, Exception err);

        private readonly IServiceMethodDispatcher serviceMethodDispatcher;
        private readonly IRpcImplInstnce rpcImplInstance;
        private readonly RoutingRule routingRule;
        private readonly IMethodSerializer methodSerializer;

        public static void Bind<TImpl>(IDataReceiver dataReceiver, TImpl instance)
            where TImpl : IRpcImplInstnce
        {
            new ServiceImplementStub(dataReceiver, typeof(TImpl), instance);
        }

        private ServiceImplementStub(IDataReceiver dataReceiver, Type serviceType, IRpcImplInstnce rpcImpl)
        {
            rpcImplInstance = rpcImpl;

            routingRule = MetaData.GetServiceRoutingRule(serviceType);
            serviceMethodDispatcher = MetaData.GetServiceMethodDispatcher(serviceType);
            methodSerializer = MetaData.GetMethodSerializer(serviceType);
            dataReceiver.RegisterImpl(this, MetaData.GetServiceId(serviceType));
        }

        private static RpcMethod DeSerializeRpcMethod(BinaryReader br, IMethodSerializer methodSerializer)
        {
            var invokeId = br.ReadUInt32();
            var method = methodSerializer.Read(br);
            method.InvokeId = invokeId;

            return method;
        }

        public void OnReceiveMessage(Mode mode, byte[] buf, int offset, MessageConsumedCallback callback)
        {
            var stream = new MemoryStream(buf);
            var br = new BinaryReader(stream);

            stream.Position = offset;

            var srcUuidLen = br.ReadInt32();
            byte[] srcUuid = null;
            if (srcUuidLen > 0)
            {
                srcUuid = br.ReadBytes(srcUuidLen);
            }
            var method = DeSerializeRpcMethod(br, methodSerializer);

            serviceMethodDispatcher.Dispatch(rpcImplInstance.SetSourceUuid(srcUuid), method, (v,e) =>
            {
                var buff = new MemoryStream(16);
                var bw = new BinaryWriter(buff);
                bw.Write(method.InvokeId);
                if (e == null)
                {
                    bw.Write(1);// return expectedly
                    methodSerializer.WriteReturn(method, bw, v);
                }
                else
                {
                    bw.Write(-1);// return unexpectedly
                }

                //todo no need to check null
                //if (callback == null)
                //{
                //    return;
                //}

                callback(buff.GetBuffer(), srcUuid, routingRule);
                //sender.Send(, serviceMetaInfo.GetReturnRoutingKey(new Guid(method.SessionId).ToString()),
                //    serviceMetaInfo.GetReturnExchangeName());
            });
        }
    }
}
