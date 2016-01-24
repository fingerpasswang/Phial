using System;
using System.Threading.Tasks;

namespace Ox.RPCBase.Server
{
    public static class ServiceDelegateEx
    {
        public static Task<TResult> InvokeT<TResult>(this ServiceDelegateStub serviceDelegateStub, uint methodId, string forwardKey, params object[] args)
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
    }

    // 针对RpcServer特定的Task用法的扩展
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
                if (e.GetType().Name.Equals("KeyLockedPreemptedException"))
                {
                    Log.Error("--------------------");
                    Log.Error("KeyLockedPreemptedException");
                    Log.Error("--------------------");
                }
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
