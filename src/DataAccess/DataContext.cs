using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using StackExchange.Redis;
using Phial;

// helper classes for code generation
namespace DataAccess
{
    // base class for holding contextLockId
    public class DelegateBase
    {
        public string dataId;
        public long contextLockId;
        public List<IUpdateDataContext> updateQueue = new List<IUpdateDataContext>();
    }

    // 
    // Accesser should implement this interface, meaning that you can invoke update while dataAccess excutes it lazily and batchly
    public interface ISubmitChangable
    {
        Task<bool> SubmitChanges();
        Task<bool> SubmitChangesWith(params object[] others);
    }

    // load context
    public interface ILoadDataContext
    {
        T ReadField<T>(string field);
        void Dispose();
    }

    // update context
    public interface IUpdateDataContext
    {
        IUpdateDataContext WriteField<T>(string field, T val);
        Task<bool> ExecuteAsync();
        void Dispose();
    }

    internal class RedisLoadDataContext : ILoadDataContext
    {
        internal static class Helper<T>
        {
            public static Func<RedisValue, T> ToValue;
        }

        static RedisLoadDataContext()
        {
            Helper<string>.ToValue = s => s;

            // uint and ulong are not supported by StackExchange.Redis
            Helper<uint>.ToValue = s => BitConverter.ToUInt32(s, 0);
            Helper<ulong>.ToValue = s => BitConverter.ToUInt64(s, 0);

            Helper<long>.ToValue = s => (long)s;
            Helper<byte[]>.ToValue = s => (byte[])s;
        }

        private readonly Dictionary<string, RedisValue> entriesDict = new Dictionary<string, RedisValue>();

        public RedisLoadDataContext(HashEntry[] entries)
        {
            foreach (var hashEntry in entries)
            {
                if (!hashEntry.Value.HasValue)continue;
                entriesDict[hashEntry.Name.ToString()] = hashEntry.Value;
            }
        }
        public T ReadField<T>(string field)
        {
            RedisValue val;
            if (!entriesDict.TryGetValue(field, out val))
            {
                return default(T);
            }

            if (Helper<T>.ToValue != null)
            {
                return Helper<T>.ToValue(val);
            }
            else
            {
                return ORMHelper.Instance.BytesToObject<T>((byte[])val);
            }
        }
        public void Dispose()
        {
            entriesDict.Clear();
        }
    }

    internal class RedisUpdateDataContext : IUpdateDataContext
    {
        internal static class Helper<T>
        {
            public static Func<T, RedisValue> ToRedisValue;
        }

        static RedisUpdateDataContext()
        {
            Helper<string>.ToRedisValue = s => (RedisValue)s;
            Helper<uint>.ToRedisValue = s => (RedisValue)BitConverter.GetBytes(s);
            Helper<ulong>.ToRedisValue = s => (RedisValue)BitConverter.GetBytes(s);
            Helper<long>.ToRedisValue = s => (RedisValue)s;
            Helper<byte[]>.ToRedisValue = s => (RedisValue)s;
        }

        public string Key { get; set; }
        public IDatabase Handle { get; set; }
        public readonly List<HashEntry> Entries = new List<HashEntry>();
        public IUpdateDataContext WriteField<T>(string field, T val)
        {
            if (Helper<T>.ToRedisValue != null)
            {
                Entries.Add(new HashEntry(field, Helper<T>.ToRedisValue(val)));
            }
            else
            {
                Entries.Add(new HashEntry(field, ORMHelper.Instance.ObjectToBytes(val)));
            }

            return this;
        }

        public IEnumerable<RedisValue> ToInputArgs()
        {
            foreach (var hashEntry in Entries)
            {
                yield return hashEntry.Name;
                yield return hashEntry.Value;
            }
        }

        public async Task<bool> ExecuteAsync()
        {
            await Handle.HashSetAsync(Key, Entries.ToArray());

            return true;
        }

        public void Dispose()
        {
            Entries.Clear();
        }
    }

    internal class MysqlLoadDataContext : ILoadDataContext
    {
        internal static class Helper<T>
        {
            public static Func<MySqlDataReader, string, T> ToValue;
        }

        static MysqlLoadDataContext()
        {
            Helper<string>.ToValue = (reader, field) => reader.GetString(field);
            Helper<uint>.ToValue = (reader, field) => reader.GetUInt32(field);
            Helper<ulong>.ToValue = (reader, field) => reader.GetUInt64(field);
            Helper<long>.ToValue = (reader, field) => reader.GetInt64(field);
        }

        public MySqlDataReader Reader { get; set; }
        public MysqlAdaptor MysqlAdaptor { get; set; }

        public T ReadField<T>(string field)
        {
            if (Helper<T>.ToValue != null)
            {
                return Helper<T>.ToValue(Reader, field);
            }
            else
            {
                return MysqlAdaptor.ReadBlob<T>(ORMHelper.Instance, Reader, field);
            }
        }
        public void Dispose()
        {
            Log.Info("Reader.Dispose");
            Reader.Dispose();
        }
    }

    internal class MysqlUpdateDataContext : IUpdateDataContext
    {
        internal static class Helper<T>
        {
            public static Func<T, object> ToValue;
        }

        static MysqlUpdateDataContext()
        {
            Helper<string>.ToValue = (v) => v;
            Helper<uint>.ToValue = (v) => v;
            Helper<ulong>.ToValue = (v) => v;
            Helper<long>.ToValue = (v) => v;
        }

        public MySqlCommand MySqlCommand { get; set; }
        private uint pos = 0;

        public IUpdateDataContext WriteField<T>(string field, T val)
        {
            if (Helper<T>.ToValue != null)
            {
                MySqlCommand.Parameters.AddWithValue((++pos).ToString(), val);
            }
            else
            {
                MySqlCommand.Parameters.AddWithValue((++pos).ToString(), ORMHelper.Instance.ObjectToBytes(val));
            }

            return this;
        }

        // todo 
        public async Task<bool> ExecuteAsync()
        {
            var ret = await Task.Factory.FromAsync(MySqlCommand.BeginExecuteNonQuery()
                , (ar => MySqlCommand.EndExecuteNonQuery(ar)), TaskCreationOptions.None);

            return true;
        }

        public void Dispose()
        {
            MySqlCommand.Dispose();
        }
    }
}
