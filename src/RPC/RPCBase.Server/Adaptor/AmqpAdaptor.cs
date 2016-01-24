using System;
using System.Collections.Generic;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RPCBase.Server
{
    public class AmqpAdaptor : IAdaptor
    {
        // rabbitMQ
        private readonly IModel mqChannel;
        private readonly EventingBasicConsumer pumpHandle;
        private readonly string privateQueueName;

        // identify current adaptor
        private readonly Guid uuid = Guid.NewGuid();

        public Guid Uuid
        {
            get { return uuid; }
        }

        // indicates that whether the adaptor is pumping data or not
        private bool isPumped;

        // hold impls and delegates registered,
        // when a msg arrived,
        // dispatch msg to specific impl or delegate
        private readonly Dictionary<string, IMessageConsumer> implements = new Dictionary<string, IMessageConsumer>();
        private readonly Dictionary<string, IMessageConsumer> delegates = new Dictionary<string, IMessageConsumer>();

        public AmqpAdaptor(string ip, int port, string user, string pass)
        {
            var mqConnectionFactory = new ConnectionFactory() { HostName = ip, Port = port, UserName = user, Password = pass };
            var mqConnection = mqConnectionFactory.CreateConnection();

            mqChannel = mqConnection.CreateModel();
            privateQueueName = mqChannel.QueueDeclare().QueueName;
            pumpHandle = new EventingBasicConsumer(mqChannel);
            pumpHandle.Received += OnReceived;
        }

        public void BeginReceive()
        {
            if (isPumped)
            {
                return;
            }
            isPumped = true;
            HashSet<string> queues = new HashSet<string>();
            lock (mqChannel)
            {
                foreach (var kv in implements)
                {
                    var rule = MetaData.GetServiceRoutingRule(kv.Key);
                    var serviceQueueName = rule.AmqpRule.GetImplQueueName();
                    //var serviceQueueName = rule.GetImplQueueName();
                    if (string.IsNullOrEmpty(serviceQueueName))
                    {
                        serviceQueueName = privateQueueName;
                    }
                    else
                    {
                        mqChannel.QueueDeclare(serviceQueueName, false, false, true, null);
                        queues.Add(serviceQueueName);
                    }

                    mqChannel.QueueBind(serviceQueueName, rule.AmqpRule.GetImplExchangeName(), rule.AmqpRule.GetImplBindingKey(), null);
                    //mqChannel.QueueBind(serviceQueueName, rule.GetImplExchangeName(), rule.GetImplBindingKey(""), null);
                }
                foreach (var kv in delegates)
                {
                    var rule = MetaData.GetServiceRoutingRule(kv.Key);
                    var returnBindingKey = rule.AmqpRule.GetReturnBindingKey(uuid);
                    //string returnBindingKey = rule.GetReturnBindingKey(uuid.ToString());
                    if (!string.IsNullOrEmpty(returnBindingKey))
                    {
                        mqChannel.QueueBind(privateQueueName, rule.AmqpRule.GetReturnExchangeName(), returnBindingKey, null);
                        //mqChannel.QueueBind(privateQueueName, rule.GetReturnExchangeName(), returnBindingKey, null);
                    }

                    // declare inter-server queue in advance
                    var serviceQueueName = rule.AmqpRule.GetImplQueueName();
                    //var serviceQueueName = rule.GetImplQueueName();
                    if (!string.IsNullOrEmpty(serviceQueueName))
                    {
                        mqChannel.QueueDeclare(serviceQueueName, false, false, true, null);
                        mqChannel.QueueBind(serviceQueueName, rule.AmqpRule.GetImplExchangeName(), rule.AmqpRule.GetImplBindingKey(), null);
                        //mqChannel.QueueBind(serviceQueueName, rule.GetImplExchangeName(), rule.GetImplBindingKey(""), null);
                    }
                }
                mqChannel.BasicConsume(privateQueueName, true, pumpHandle);
                foreach (string queue in queues)
                {
                    mqChannel.BasicConsume(queue, true, pumpHandle);
                }
            }
        }

        private void OnReceived(object sender, BasicDeliverEventArgs ea)
        {
            var topic = ea.RoutingKey.Split('.');
            var sid = topic[1];
            var mode = MetaData.GetMode(topic[2]);//todo change string to char
            var toQuery = mode == Mode.Return ? delegates : implements;

            IMessageConsumer msgConsumer;
            
            if (toQuery.TryGetValue(sid, out msgConsumer))
            {
                //todo srcUuid dup one, and unpack at RPC layer
                msgConsumer.OnReceiveMessage(mode, ea.Body, 0, (buffer, dstUuid, rule) =>
                {
                    lock (mqChannel)
                    {
                        mqChannel.BasicPublish(rule.AmqpRule.GetReturnExchangeName(), rule.AmqpRule.GetReturnRoutingKey(dstUuid), null, buffer);
                        //mqChannel.BasicPublish(exchangeName, routingKey, null, buffer);
                    }
                });
            }
        }

        public void RegisterDelegate(IMessageConsumer consumer, string serviceId)
        {
            if (isPumped)
            {
                return;
            }
            //messageIo.SetSessionId(uuid.ToByteArray());
            delegates[serviceId] = consumer;
        }

        public void RegisterImpl(IMessageConsumer consumer, string serviceId)
        {
            if (isPumped)
            {
                return;
            }
            implements[serviceId] = consumer;
        }

        public void Send(byte[] buffer, byte[] dstUuid, RoutingRule routingRule)
        {
            lock (mqChannel)
            {
                //todo uuid.ToString() consumes too much
                mqChannel.BasicPublish(routingRule.AmqpRule.GetDelegateExchangeName(), routingRule.AmqpRule.GetDelegateRoutingKey(dstUuid), null, buffer);
            }
        }
    }
}
