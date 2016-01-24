using System;

namespace RPCBase
{
    public class RoutingRule
    {
        public delegate TR MetaInfoFunc<in T1, in T2, out TR>(T1 arg1, T2 arg2);
        public delegate TR MetaInfoFunc<in T1, out TR>(T1 arg1);
        public delegate TR MetaInfoFunc<out TR>();

        public static string DistrictsName { get; set; }

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
            /// <summary>
            /// DistrictsName -> uuid -> GateId;
            /// </summary>
            public MetaInfoFunc<string, int> ServiceId { private get; set; }
            public int GetServiceId()
            {
                return ServiceId != null ? ServiceId(DistrictsName) : 0;
            }
        }

        public MqttRoutingRule MqttRule { get; set; }
        public GateRoutingRule GateRule { get; set; }
    }
}
