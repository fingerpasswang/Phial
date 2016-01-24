using System;

namespace RPCBase
{
    // todo not ensure thread-safe yet
    // a handle for one async invoke
    public class InvokeOperation
    {
        public uint MethodId;
        public uint InvokeId;
        public bool IsComplete { get; private set; }
        public InvokeOperationException Exception { get; protected set; }
        private Action<InvokeOperation> cb;

        public Action<InvokeOperation> Callback
        {
            set
            {
                if (IsComplete)
                {
                    value(this);
                }
                else
                {
                    cb = value;
                }
            }
        }

        public virtual void SetResult(object rst)
        {
            IsComplete = true;
            if (cb != null)
            {
                cb(this);
            }
        }

        public virtual void SetException(InvokeOperationException e)
        {
            Exception = e;
            if (cb != null)
            {
                cb(this);
            }
        }
    }

    public class InvokeOperation<T> : InvokeOperation
    {
        public T Result { get; private set; }
        private Action<InvokeOperation<T>> cb;
        public new Action<InvokeOperation<T>> Callback
        {
            set
            {
                if (IsComplete)
                {
                    value(this);
                }
                else
                {
                    cb = value;
                }
            }
        }

        public override void SetResult(object rst)
        {
            try
            {
                Result = (T) rst;
                base.SetResult(rst);
            }
            catch (Exception e)
            {
                SetException(new InvokeOperationException(e));
            }
            finally
            {
                if (cb != null)
                {
                    cb(this);
                }
            }
        }
        public override void SetException(InvokeOperationException e)
        {
            base.SetException(e);
            if (cb != null)
            {
                cb(this);
            }
        }
    }
}
