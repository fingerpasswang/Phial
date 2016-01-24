using System;
using System.IO;
using System.Threading.Tasks;
using Phial.Log;

namespace RPCBase.Server
{
    public static class ServiceDelegateEx
    {
        public static Task<TResult> InvokeT<TResult>(this ServiceDelegateStub serviceDelegateStub, uint methodId, byte[] forwardKey, params object[] args)
        {
            var op = serviceDelegateStub.Invoke<TResult>(methodId, forwardKey, args);
            var tcs = new TaskCompletionSource<TResult>();

            op.Callback = operation =>
            {
                if (!operation.IsComplete)
                {
                    tcs.SetException(new Exception());
                }
                else
                {
                    tcs.SetResult(operation.Result);
                }
            };

            return tcs.Task;
        }

        public static void Multicast(this ServiceDelegateStub serviceDelegateStub, uint methodId, int forwardId, params object[] args)
        {
            var forwarder = serviceDelegateStub.DataSender as IMulticastSender;

            if (forwarder == null)
            {
                return;
            }

            var buffer = new MemoryStream(32);
            var bufferWriter = new BinaryWriter(buffer);

            var uuidBytes = serviceDelegateStub.DataSender.Uuid.ToByteArray();

            bufferWriter.Write(uuidBytes.Length);
            bufferWriter.Write(uuidBytes);
            bufferWriter.Write(0);
            serviceDelegateStub.MethodSerializer.Write(methodId, args, bufferWriter);

            forwarder.MulticastSend(buffer.GetBuffer(), forwardId, serviceDelegateStub.RoutingRule);
        }
    }

    // extend for task async/await
    public abstract class ServiceMethodDispatcherEx
    {
        protected void DoContinue<T>(Task<T> t, ServiceImplementStub.SendResult cont)
        {
            T value;
            try
            {
                value = t.Result;
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                //if (e.GetType().Name.Equals("KeyLockedPreemptedException"))
                //{
                //    Log.Error("--------------------");
                //    Log.Error("KeyLockedPreemptedException");
                //    Log.Error("--------------------");
                //}
                cont(null, e);
                return;
            }
            cont(value, null);
        }

        protected void DoContinue(Task t, ServiceImplementStub.SendResult cont)
        {
            cont(null, t.Exception);
        }
    }
}
