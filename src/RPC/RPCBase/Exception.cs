using System;

namespace RPCBase
{
    public class InvokeOperationException : Exception
    {
        private Exception normalExpException;

        public InvokeOperationException(Exception e)
        {
            normalExpException = e;
        }

        public override string ToString()
        {
            return normalExpException.ToString();
        }
    }
}
