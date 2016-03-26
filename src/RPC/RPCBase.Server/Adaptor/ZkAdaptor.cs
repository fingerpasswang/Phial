using System;
using System.Collections.Generic;
using System.Linq;
using ZooKeeperNet;
using Phial;

namespace RPCBase.Server
{
    public class ZkAdaptor : IAdaptor 
    {
        // identify current adaptor
        private readonly Guid uuid = Guid.NewGuid();

        public Guid Uuid
        {
            get { return uuid; }
        }

        // hold impls and delegates registered,
        // when a msg arrived,
        // dispatch msg to specific impl or delegate
        private readonly Dictionary<string, IMessageConsumer> implements = new Dictionary<string, IMessageConsumer>();
        private readonly Dictionary<string, IMessageConsumer> delegates = new Dictionary<string, IMessageConsumer>();

        private ZooKeeper handle;
        private ZkWatcher watcher;
        private readonly Dictionary<string, Election> electionDict = new Dictionary<string,Election>();

        public ZkAdaptor()
        {
            watcher = new ZkWatcher()
            {
                DataChangedHandler = OnDataChanged,
            };

            // zk handle
            // zkServer addr, session timeout, watcher
            handle = new ZooKeeper("192.168.0.103:2181,192.168.0.103:2182,192.168.0.103:2183,192.168.0.103:2184,192.168.0.103:2185", new TimeSpan(0, 0, 0, 50000), watcher);

            try
            {
                // create root node
                // no ACL
                // Persistent node
                handle.Create(RoutingRule.ZkRoutingRule.GetServiceRoot(), null, Ids.OPEN_ACL_UNSAFE, CreateMode.Persistent);
            }
            catch (KeeperException.NodeExistsException e)
            {
                // ignore
            }

            watcher.Handle = handle;
        }

        public void RegisterDelegate(IMessageConsumer consumer, string serviceId)
        {
            var rule = MetaData.GetServiceRoutingRule(serviceId);
            if (rule == null)
            {
                throw new Exception();
            }

            var path = rule.ZkRule.GetServicePath();

            try
            {
                handle.Create(path, null, Ids.OPEN_ACL_UNSAFE, CreateMode.Persistent);
            }
            catch (KeeperException.NodeExistsException e)
            {
                // ignore
            }

            delegates[path] = consumer;
        }

        public void RegisterImpl(IMessageConsumer consumer, string serviceId)
        {
            var rule = MetaData.GetServiceRoutingRule(serviceId);
            if (rule == null)
            {
                throw new Exception();
            }

            var path = rule.ZkRule.GetServicePath();

            try
            {
                handle.Create(path, null, Ids.OPEN_ACL_UNSAFE, CreateMode.Persistent);
            }
            catch (KeeperException.NodeExistsException e)
            {
                // ignore
            }

            implements[path] = consumer;
        }

        public void Send(byte[] buffer, byte[] dstUuid, RoutingRule routingRule)
        {
            // modify data associated with a node
            // no need to concern version , at present
            var ret = handle.SetData(routingRule.ZkRule.GetServicePath(), buffer, -1);
        }

        public void BeginReceive()
        {
            foreach (var implPath in implements.Keys)
            {
                handle.GetData(implPath, watcher, null);
            }
        }

        public void Identity(string roundName)
        {
            if (electionDict.ContainsKey(roundName))
            {
                return;
            }

            var roundsRoot = string.Format("/{0}Rounds", RoutingRule.DistrictsName);
            var thisRound = string.Format("{0}/{1}", roundsRoot, roundName);
            try
            {
                handle.Create(roundsRoot, null, Ids.OPEN_ACL_UNSAFE, CreateMode.Persistent);
            }
            catch (KeeperException.NodeExistsException e)
            {
                // ignore
            }

            try
            {
                handle.Create(thisRound, null, Ids.OPEN_ACL_UNSAFE, CreateMode.Persistent);
            }
            catch (KeeperException.NodeExistsException e)
            {
                // ignore
            }

            var me = string.Format("{0}/{1}-n_", thisRound, uuid);
            var ret = handle.Create(me, null, Ids.OPEN_ACL_UNSAFE, CreateMode.EphemeralSequential);

            var elect = new Election(handle, thisRound, roundName, Election.PathToSeq(ret));

            electionDict[roundName] = elect;
            elect.AttempToBecomeLeader();
        }

        public bool IsLeader(string roundName)
        {
            Election election = null;
            if (!electionDict.TryGetValue(roundName, out election))
            {
                throw new Exception("no round:"+roundName);
            }

            return election.IsLeader;
        }

        private void OnDataChanged(string path, byte[] buffer)
        {
            IMessageConsumer msgConsumer;

            if (implements.TryGetValue(path, out msgConsumer))
            {
                msgConsumer.OnReceiveMessage(Mode.Notify, buffer, 0, null);
            }
        }

        class Election : IWatcher
        {
            private readonly ZooKeeper handle;
            private readonly string roundRoot;
            private readonly string round;
            private readonly int selfSeq;

            public bool IsLeader { get; private set; }

            public Election(ZooKeeper handle, string roundRoot, string round, int selfSeq)
            {
                this.handle = handle;
                this.roundRoot = roundRoot;
                this.round = round;
                this.selfSeq = selfSeq;
            }

            public void AttempToBecomeLeader()
            {
                var children = handle.GetChildren(roundRoot, false);

                OnRoundChildrenChanged(children);
            }

            public void OnRoundChildrenChanged(IEnumerable<string> children)
            {
                var seqs = children
                    .Select(p=>new KeyValuePair<string, int>(p, PathToSeq(p)))
                    .Where(pair=>pair.Value!=-1)
                    .OrderBy(pair => pair.Value)
                    .ToArray();

                string toWatch = null;

                for (int i = 0; i < seqs.Length-1; i++)
                {
                    if (seqs[i + 1].Value == selfSeq)
                    {
                        toWatch = seqs[i].Key;
                        break;
                    }
                }

                if (toWatch == null)
                {
                    Log.Info("nothing to watch , since self is leader");
                    IsLeader = true;
                }
                else
                {
                    Log.Info("watch {0}", toWatch);
                    handle.Exists(roundRoot+'/'+toWatch, this);
                }
            }

            public void Process(WatchedEvent @event)
            {
                if (@event.Type == EventType.NodeDeleted)
                {
                    Log.Info("watching child {0} is deleted", @event.Path);
                    AttempToBecomeLeader();
                }
            }

            public static int PathToSeq(string path)
            {
                var ret = path.Split('/');
                var last = ret[ret.Length - 1].Split('_');
                int seq = -1;

                if (!int.TryParse(last[last.Length - 1], out seq))
                {
                    return -1;
                }

                return seq;
            }
        }

        class ZkWatcher : IWatcher
        {
            public ZooKeeper Handle { get; set; }
            public Action<string, byte[]> DataChangedHandler { get; set; }

            public ZkWatcher()
            {
            }

            public void Process(WatchedEvent @event)
            {
                if (@event.Type == EventType.NodeDataChanged)
                {
                    Log.Info("Process path:{0}", @event.Path);

                    var ret3 = Handle.GetData(@event.Path, this, null);

                    DataChangedHandler(@event.Path, ret3);
                }

            }
        }
    }
}
