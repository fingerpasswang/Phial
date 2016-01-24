using System.IO;

namespace RPCBase
{
    public interface IMethodSerializer
    {
        RpcMethod Read(BinaryReader br);
        void Write(uint methodId, object[] args, BinaryWriter bw);
        object ReadReturn(uint methodId, BinaryReader br);
        void WriteReturn(RpcMethod method, BinaryWriter bw, object value);
    }

    public enum SerializeObjectMark
    {
        Common = 255,
        IsNull = 0,
    }

    public class RpcMethod
    {
        public uint InvokeId;
        public uint MethodId;
        public object[] Args;
    }
}
