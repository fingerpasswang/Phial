using System;
using System.Collections.Generic;
using System.Threading;
using MqttLib;
using Phial.Log;

namespace RPCBase.Client
{
    public sealed class MqttAdaptor : IAdaptor, IPollable, IDisposable
    {
        class DefferedJob
        {
            public byte[] Buffer;
            public Mode Mode;
            public IMessageConsumer MessageConsumer;
        }

        // hold impls and delegates registered,
        // when a msg arrived,
        // dispatch msg to specific impl or delegate
        private readonly Dictionary<string, IMessageConsumer> implements = new Dictionary<string, IMessageConsumer>();
        private readonly Dictionary<string, IMessageConsumer> delegates = new Dictionary<string, IMessageConsumer>();

        // deffered to subscribe when connected
        private readonly Dictionary<string, bool> toSubscribe = new Dictionary<string, bool>();

        // used for subscription when reconnected
        private readonly Dictionary<string, bool> subscriptions = new Dictionary<string, bool>();

        // used for poll request of upper layer
        private readonly Queue<DefferedJob> defferedJobs = new Queue<DefferedJob>(10);
        private bool connectedDefferedNotify = false;
        private bool disconnectedDefferedNotify = false;

        private IMqtt handle;

        // identify current adaptor
        // todo multi adaptor might need one uuid
        private readonly Guid uuid = Guid.NewGuid();
        public Guid Uuid
        {
            get { return uuid; }
        }

        public delegate void ConnectedHandler();
        public delegate void DisconnectedHandler();

        public ConnectedHandler Connected;
        public DisconnectedHandler Disconnected;

        public Guid GetUuid()
        {
            return uuid;
        }

        // hold for reconnecting
        private readonly string connStr;
        private string clientId;
        private readonly string user;
        private readonly string pass;

        public MqttAdaptor(string connIp, int port, string user, string pass)
        {
            connStr = string.Format("tcp://{0}:{1}", connIp, port);
            clientId = string.Format("client_{0}", uuid);
            this.user = user;
            this.pass = pass;

            InitHandle();

            Log.Info("MqttAdaptor constructed");
        }

        void InitHandle()
        {
            try
            {
                handle = MqttClientFactory.CreateClient(connStr, clientId, user, pass);
                Log.Info("MqttAdaptor handle constructed");
            }
            catch (Exception e)
            {
                Log.Error("MqttAdaptor InitHandle construct throw exp:{0}", e);
                throw;
            }

            handle.Connected = OnConnected;
            handle.ConnectionLost = OnDisconnected;
            handle.PublishArrived = OnReceived;

            Log.Info("MqttAdaptor InitHandle");
        }

        private bool OnReceived(object sender, PublishArrivedArgs ea)
        {
            Log.Info("Received Message");
            Log.Info("Topic: " + ea.Topic);
            Log.Info("MqttAdaptor.OnReceived, topic={0}", ea.Topic);

            var topic = ea.Topic.Split('/');
            var sid = topic[1];
            var mode = MetaData.GetMode(topic[2]);
            var toQuery = mode == Mode.Return ? delegates : implements;
            IMessageConsumer msgConsumer;

            if (toQuery.TryGetValue(sid, out msgConsumer))
            {
                var job = new DefferedJob()
                {
                    Buffer = (byte[])ea.Payload.TrimmedBuffer.Clone(),
                    Mode = mode,
                    MessageConsumer = msgConsumer,
                };

                lock (defferedJobs)
                {
                    defferedJobs.Enqueue(job);
                }
            }

            return true;
        }

        // Poll、RegisterDelegate、RegisterImpl、BeginReceive
        // all scheduled by user thread

        // OnConnected is scheduled by background thread
        public void Poll()
        {
            if (connectedDefferedNotify && Connected != null)
            {
                connectedDefferedNotify = false;

                Log.Info("Poll before Connected callback");
                Connected();
            }
            if (disconnectedDefferedNotify && Connected != null)
            {
                disconnectedDefferedNotify = false;

                Log.Info("Poll before Disconnected callback");
                Disconnected();
            }

            var toHandle = new List<DefferedJob>();
            lock (defferedJobs)
            {
                while (defferedJobs.Count > 0)
                {
                    toHandle.Add(defferedJobs.Dequeue());
                }
            }
            foreach (var defferedJob in toHandle)
            {
                defferedJob.MessageConsumer.OnReceiveMessage(defferedJob.Mode, defferedJob.Buffer, 0, null);
            }
        }

        // todo Critical Section 
        // user thread should ensure both of the two entrance thread-safely
        public void RegisterDelegate(IMessageConsumer consumer, string serviceId)
        {
            delegates[serviceId] = consumer;
            var rule = MetaData.GetServiceRoutingRule(serviceId);
            if (rule == null)
            {
                throw new Exception();
            }

            var key = rule.MqttRule.GetSubscribeKey(uuid);
            if (!handle.IsConnected)
            {
                toSubscribe[key] = true;
                return;
            }

            try
            {
                handle.Subscribe(key, QoS.AtLeastOnce);
                subscriptions[key] = true;
            }
            catch (Exception e)
            {
                Log.Error("MqttAdaptor RegisterDelegate throw exp:{0}", e);
                throw;
            }
        }

        public void RegisterImpl(IMessageConsumer impl, string serviceId)
        {
            implements[serviceId] = impl;

            var rule = MetaData.GetServiceRoutingRule(serviceId);
            if (rule == null)
            {
                throw new Exception();
            }

            var key = rule.MqttRule.GetSubscribeKey(uuid);
            if (!handle.IsConnected)
            {
                toSubscribe[key] = true;
                return;
            }

            try
            {
                handle.Subscribe(key, QoS.AtLeastOnce);
                subscriptions[key] = true;
            }
            catch (Exception e)
            {
                Log.Error("MqttAdaptor RegisterImpl throw exp:{0}", e);
                throw;
            }
        }

        // begin to connect
        public void BeginReceive()
        {
            ThreadPool.QueueUserWorkItem((o) =>
            {
                Log.Info("BeginReceive handle.Connect(true);");
                try
                {
                    handle.Connect(true);
                }
                catch (Exception e)
                {
                    // todo event that connect failed must be thrown to upper layer
                    Log.Error("MqttAdaptor BeginReceive throw exp:{0}", e);
                    throw;
                }
            });
        }

        // reconnect
        // todo easy one implementation 
        // just drop old handle
        public void Reconnect()
        {
            ThreadPool.QueueUserWorkItem((o) =>
            {
                Log.Info("Reconnect handle.Connect(false);");
                try
                {
                    handle = null;
                    clientId = string.Format("client_{0}", Guid.NewGuid());
                    InitHandle();
                    handle.Connect(false);
                    Log.Info("Reconnect handle.Connect(false); complete");
                }
                catch (Exception e)
                {
                    Log.Error("MqttAdaptor Reconnect throw exp:{0}", e);
                }
            });
        }

        // todo it is registered when connected conventionally , thus no need to lock
        private void OnConnected(object sender, EventArgs e)
        {
            Log.Info("MqttAdaptor OnConnected");

            try
            {
                // re-subsribe when reconnect
                foreach (var kv in subscriptions)
                {
                    handle.Subscribe(kv.Key, QoS.AtLeastOnce);
                    Log.Info("subscriptions {0}", kv.Key);
                    //subscriptions[kv.Key] = !kv.Value;
                }

                foreach (var kv in toSubscribe)
                {
                    handle.Subscribe(kv.Key, QoS.AtLeastOnce);
                    subscriptions[kv.Key] = true;
                    Log.Info("toSubscribe {0}", kv.Key);
                }

                toSubscribe.Clear();

                // no need to synchronize connectedDefferedNotify
                connectedDefferedNotify = true;
            }
            catch (Exception exp)
            {
                Log.Error("MqttAdaptor OnConnected throw exp:{0}", exp);
                throw;
            }
        }

        private void OnDisconnected(object sender, EventArgs e)
        {
            Log.Info("MqttAdaptor OnDisconnected");

            disconnectedDefferedNotify = true;
        }

        public void Send(byte[] buffer, byte[] dstUuid, RoutingRule routingRule)
        {
            // mqttclient push msg to a thread-safe queue
            try
            {
                //todo uuid.ToString() 
                //todo dstUuid arg have no use here
                handle.Publish(routingRule.MqttRule.GetPublishKey(uuid), new MqttPayload(buffer, 0), QoS.BestEfforts, false);
            }
            catch (Exception e)
            {
                Log.Error("MqttAdaptor Send throw exp:{0}", e);
            }
        }

        public void Dispose()
        {
            //foreach (var kv in subscriptions)
            //{
            //    try
            //    {
            //        handle.Unsubscribe(new string[] { kv.Key });
            //    }
            //    catch (Exception e)
            //    {
            //        Log.Error("MqttAdaptor Dispose throw exp:{0} key:{1}", e, kv.Key);
            //    }
            //}

            handle.Disconnect();
        }
    }
}
