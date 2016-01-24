
using System;

namespace Ox.RPCBase
{
    public class RoutingRule
    {
        public delegate TR MetaInfoFunc<in T1, in T2, out TR>(T1 arg1, T2 arg2);
        public delegate TR MetaInfoFunc<in T1, out TR>(T1 arg1);
        public delegate TR MetaInfoFunc<out TR>();

        public static string DistrictsName { get; set; }

        public class AmqpRoutingRule
        {
            /// <summary>
            /// DistrictsName -> uuid -> DelegateRoutingKey;
            /// </summary>
            public MetaInfoFunc<string, string, string> DelegateRoutingKey { private get; set; }
            public string GetDelegateRoutingKey(Guid uuid)
            {
                return DelegateRoutingKey != null ? DelegateRoutingKey(DistrictsName, uuid.ToString()) : "";
            }
            public string GetDelegateRoutingKey(byte[] uuid)
            {
                return DelegateRoutingKey != null ? DelegateRoutingKey(DistrictsName, new Guid(uuid).ToString()) : "";
            }

            /// <summary>
            /// DelegateExchangeName;
            /// </summary>
            public MetaInfoFunc<string> DelegateExchangeName { private get; set; }
            public string GetDelegateExchangeName()
            {
                return DelegateExchangeName != null ? DelegateExchangeName() : "";
            }

            /// <summary>
            /// DistrictsName -> uuid -> ImplBindingKey;
            /// </summary>
            public MetaInfoFunc<string, string, string> ImplBindingKey { private get; set; }
            public string GetImplBindingKey(Guid uuid)
            {
                return ImplBindingKey != null ? ImplBindingKey(DistrictsName, uuid.ToString()) : "";
            }
            public string GetImplBindingKey()
            {
                return ImplBindingKey != null ? ImplBindingKey(DistrictsName, "") : "";
            }

            /// <summary>
            /// ImplExchangeName;
            /// </summary>
            public MetaInfoFunc<string> ImplExchangeName { private get; set; }
            public string GetImplExchangeName()
            {
                return ImplExchangeName != null ? ImplExchangeName() : "";
            }

            /// <summary>
            /// DistrictsName -> DelegateQueueName;
            /// </summary>
            public MetaInfoFunc<string, string> ImplQueueName { private get; set; }
            public string GetImplQueueName()
            {
                return ImplQueueName != null ? ImplQueueName(DistrictsName) : "";
            }

            /// <summary>
            /// DistrictsName -> uuid -> ReturnBindingKey;
            /// </summary>
            public MetaInfoFunc<string, string, string> ReturnBindingKey { private get; set; }
            public string GetReturnBindingKey(Guid uuid)
            {
                return ReturnBindingKey != null ? ReturnBindingKey(DistrictsName, uuid.ToString()) : "";
            }

            /// <summary>
            /// DistrictsName -> uuid -> ReturnRoutingKey;
            /// </summary>
            public MetaInfoFunc<string, string, string> ReturnRoutingKey { private get; set; }
            public string GetReturnRoutingKey(Guid uuid)
            {
                return ReturnRoutingKey != null ? ReturnRoutingKey(DistrictsName, uuid.ToString()) : "";
            }
            public string GetReturnRoutingKey(byte[] uuid)
            {
                return ReturnRoutingKey != null ? ReturnRoutingKey(DistrictsName, new Guid(uuid).ToString()) : "";
            }

            /// <summary>
            /// ReturnExchangeName;
            /// </summary>
            public MetaInfoFunc<string> ReturnExchangeName { private get; set; }
            public string GetReturnExchangeName()
            {
                return ReturnExchangeName != null ? ReturnExchangeName() : "";
            }

            /// <summary>
            /// DistrictsName -> ReturnQueueName;
            /// </summary>
            public MetaInfoFunc<string, string> ReturnQueueName { private get; set; }
            public string GetReturnQueueName()
            {
                return ReturnQueueName != null ? ReturnQueueName(DistrictsName) : "";
            }
        }

        public class MqttRoutingRule
        {
            /// <summary>
            /// DistrictsName -> uuid -> DelegateRoutingKey;
            /// </summary>
            public MetaInfoFunc<string, string, string> PublishKey { private get; set; }
            public string GetPublishKey(Guid uuid)
            {
                return PublishKey != null ? PublishKey(DistrictsName, uuid.ToString()) : "";
            }

            /// <summary>
            /// DistrictsName -> uuid -> ImplBindingKey;
            /// </summary>
            public MetaInfoFunc<string, string, string> SubscribeKey { private get; set; }
            public string GetSubscribeKey(Guid uuid)
            {
                return SubscribeKey != null ? SubscribeKey(DistrictsName, uuid.ToString()) : "";
            }
        }

        public class GateRoutingRule
        {
            
        }

        public class ZkRoutingRule
        {
            
        }

        public AmqpRoutingRule AmqpRule { get; set; }
        public MqttRoutingRule MqttRule { get; set; }
        public GateRoutingRule GateRule { get; set; }
        public ZkRoutingRule ZkRule { get; set; }
    }
}
