using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using StackExchange.Redis;
using Phial;

namespace DataAccess
{
    public struct CacheTask
    {
        public ulong Pid;
    }

    // interlayer between redisClient and dataAccess
    public class RedisAdaptor
    {
        private static ConnectionMultiplexer muxerInstance;

        public IDatabase Handle { get; private set; }
        public ConnectionMultiplexer Multiplexer { get { return muxerInstance; } }

        // SHA1 of lockId script
        private byte[] updateScriptSha; 

        public RedisAdaptor(string ip, int port)
        {
            if (muxerInstance == null)
            {
                muxerInstance = ConnectionMultiplexer.Connect(string.Format("{0}:{1}", ip, port));
            }

            Handle = muxerInstance.GetDatabase();

            var script = Load("update_multikeys_multifields.lua");

            //todo a hack way .. to be changed later
            var server = muxerInstance.GetServer(muxerInstance.GetEndPoints()[0]);

            updateScriptSha = server.ScriptLoad(script);
        }

        public RedisAdaptor(List<EndPoint> endPoints)
        {
            var config = new ConfigurationOptions()
            {
                AllowAdmin = true,
            };

            foreach (var endPoint in endPoints)
            {
                config.EndPoints.Add(endPoint);
            }

            muxerInstance = ConnectionMultiplexer.Connect(config);

            Handle = muxerInstance.GetDatabase();

            var script = Load("update_multikeys_multifields.lua");

            //todo a hack way .. to be changed later
            foreach (var endPoint in muxerInstance.GetEndPoints())
            {
                var server = muxerInstance.GetServer(endPoint);

                updateScriptSha = server.ScriptLoad(script);
            }

            Handle.StringSet("test", "111");
        }

        public async Task<ILoadDataContext> Query(string queryCmd, string field)
        {
            var result = await Handle.HashGetAsync(queryCmd, field);

            return new RedisLoadDataContext(new[] {new HashEntry(field, result),});
        }
        public async Task<ILoadDataContext> QueryAll(string queryCmd)
        {
            var result = await Handle.HashGetAllAsync(queryCmd);

            // todo just a temporary trick
            if (result.Length == 0||(result.Length == 1 &&result[0].Name.Equals("_lockId")))
            {
                return null;
            }
            return new RedisLoadDataContext(result);
        }

        public IUpdateDataContext Update(string key)
        {
             return new RedisUpdateDataContext()
             {
                 Key = key,
                 Handle = Handle,
             };
        }

        static readonly DateTime start = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1));

        public static double GetNowUnixTimeDouble()
        {
            return (DateTime.Now - start).TotalSeconds;
        }

        public async Task<bool> UpdateWork(object id)
        {
            RedisValue taskMember = string.Format("task:u:{0}", id);
            RedisKey taskKey = string.Format("task:{0}", taskMember.GetHashCode() % 4);
            var timestamp = GetNowUnixTimeDouble();

            var result = await Handle.SortedSetAddAsync(taskKey, taskMember, timestamp);
            return result;
        }

        public async Task<List<string>> GetTasks(string key, uint num)
        {
            var vals = await Handle.SortedSetRangeByRankAsync(key, 0, num - 1);
            var tasks = new List<string>(vals.Select(v => (string)v));

            return tasks;
        }

        public async Task<bool> DeleteTask(string key, string task)
        {
            return await Handle.SortedSetRemoveAsync(key, task);
        }

        public async Task<long> LockKey(string key)
        {
            // todo better use uuid for lockid, instead of timestamp
            var lockId = DateTime.Now.Ticks;

            Log.Info("LockKey key:{0} lockId:{1}", key, lockId);
            await Handle.HashSetAsync(key, new HashEntry[] {new HashEntry("_lockId", lockId), });

            return lockId;
        }

        // multi-keys updates, atomically
        public async Task<bool> SubmitChanges(string[] keys, long[] lockId, List<IUpdateDataContext>[] updateQueues)
        {
            var inputKeys = keys.Select(k => (RedisKey) k).ToArray();
            var inputArgs = lockId.Select(l => (RedisValue) l).Concat(
                updateQueues.Select(UpdateQueueToArgs)
                    .Aggregate(new List<RedisValue>() as IEnumerable<RedisValue>, (cur, next) => cur.Concat(next))).ToArray();

            //Console.WriteLine("SubmitChanges Keys={0} Args={1}"
            //    , string.Join(",", inputKeys)
            //    , string.Join(",", inputArgs));

            var ret = await Handle.ScriptEvaluateAsync(
                updateScriptSha
                , inputKeys
                , inputArgs);

            Log.Info("SubmitChanges ret={0}", ret);

            if (ret.ToString() != "0")
            {
                throw new KeyLockedPreemptedException(ret.ToString());
            }
            //await Task.WhenAll(updates.Select(u=>u.ExecuteAsync()));

            return true;
            //var ret = await Handle.ScriptEvaluate("", new RedisKey[] { key }, )
        }

        private static IEnumerable<RedisValue> UpdateQueueToArgs(List<IUpdateDataContext> queue)
        {
            var updates =
                queue.Select(u => ((RedisUpdateDataContext) u).ToInputArgs())
                    .Aggregate(new List<RedisValue>() as IEnumerable<RedisValue>, (cur, next) => cur.Concat(next)).ToList();

            yield return updates.Count/2;

            foreach (var redisValue in updates)
            {
                yield return redisValue;
            }
        }

        private static string Load(string path)
        {
            var thisExe = Assembly.GetExecutingAssembly();
            var stream = thisExe.GetManifestResourceStream("Ox.DbAccess." + path);
            var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }
    }

    public class KeyLockedPreemptedException : Exception
    {
        public KeyLockedPreemptedException(string msg)
            :base(string.Format("key has been preempted msg:{0}", msg))
        {
            
        }
    }
}
