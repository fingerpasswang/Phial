using System;
using System.Reflection;
using System.Threading;
using AltSerialize;
using Phial;

// helper classes for AltSerialize
namespace DataAccess
{
    // for registering types
    public interface ITypeRegister
    {
        void Register(Type t, int id);
        void RegisterEnum<T>(int id);
        void RegisterAssembly(Assembly ass);
    }

    public class Register4AltSerializer : ITypeRegister
    {
        public void Register(Type t, int id)
        {
            AltSerializer.AddType(t, id);
        }

        public void RegisterEnum<T>(int id)
        {
            AltSerializer.AddEnumType<T>(id);
        }

        public void RegisterAssembly(Assembly ass)
        {
            AltSerializer.Assembly = ass;
        }
    }

    // one DbSerializer instance per thread
    internal class DbSerializer
    {
        Serializer serialize = new Serializer();

        //custom type
        public byte[] CustomTypeToBytes(object o, Type type)
        {
            byte[] targetData = serialize.Serialize(o, type);
            return targetData;
        }

        public void BytesToCustomType(byte[] buffer, out object o)
        {
            o = serialize.Deserialize(buffer);
        }
    }

    internal class ORMHelper
    {
        // ThreadLocal is thread-safe
        // multi instances
        private static ThreadLocal<ORMHelper> instance = new ThreadLocal<ORMHelper>(() => new ORMHelper());
        public static ORMHelper Instance
        {
            get
            {
                return instance.Value;
            }
        }

        private ORMHelper()
        {
            Log.Info("ORMHelper constructed threadId={0}", Thread.CurrentThread.ManagedThreadId);
        }

        private readonly DbSerializer serializer = new DbSerializer();

        public byte[] ObjectToBytes<T>(T obj)
        {
            var data = serializer.CustomTypeToBytes(obj, obj.GetType());

            return data;
        }

        public T BytesToObject<T>(byte[] buffer)
        {
            object o = null;

            serializer.BytesToCustomType(buffer, out o);

            return (T)o;
        }
    }
}
