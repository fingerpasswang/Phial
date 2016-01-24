using System;
using System.Collections.Generic;

namespace RPCBase
{
    // helper code for RPC register
    // dispatcher and methodSerializer could be registered externally
    // logic code should not have a concern
    public static class MetaData
    {
        private static readonly Dictionary<Type, string> TypeToServiceId = new Dictionary<Type, string>();
        private static readonly Dictionary<Type, IServiceMethodDispatcher> TypeToServiceMethodDispatcher = new Dictionary<Type, IServiceMethodDispatcher>();
        private static readonly Dictionary<Type, IMethodSerializer> TypeToMethodSerializer = new Dictionary<Type, IMethodSerializer>();

        public static string GetServiceId(Type service)
        {
            return TypeToServiceId[service];
        }
        public static void SetServiceId(Type service, string serviceId)
        {
            TypeToServiceId[service] = serviceId;
        }

        public static IServiceMethodDispatcher GetServiceMethodDispatcher(Type service)
        {
            return TypeToServiceMethodDispatcher[service];
        }
        public static void SetServiceMethodDispatcher(Type service, IServiceMethodDispatcher serviceMethodDispatcher)
        {
            TypeToServiceMethodDispatcher[service] = serviceMethodDispatcher;
        }

        public static IMethodSerializer GetMethodSerializer(Type service)
        {
            return TypeToMethodSerializer[service];
        }
        public static void SetMethodSerializer(Type service, IMethodSerializer methodSerializer)
        {
            TypeToMethodSerializer[service] = methodSerializer;
        }

        public static Mode GetMode(string mode)
        {
            if (mode.EndsWith("notify"))
                return Mode.Notify;
            if (mode.EndsWith("sync"))
                return Mode.Sync;
            if (mode.EndsWith("invoke"))
                return Mode.Invoke;
            if (mode.EndsWith("return"))
                return Mode.Return;

            return Mode.Invoke;
        }

        public static Mode GetMode(byte mode)
        {
            if (mode == 2)
                return Mode.Notify;
            if (mode == 3)
                return Mode.Sync;
            if (mode == 0)
                return Mode.Invoke;
            if (mode == 1)
                return Mode.Return;

            return Mode.Invoke;
        }

        private static readonly Dictionary<string, RoutingRule> ServiceIdToRoutingRule = new Dictionary<string, RoutingRule>();

        public static RoutingRule GetServiceRoutingRule(string serviceId)
        {
            return ServiceIdToRoutingRule[serviceId];
        }
        public static RoutingRule GetServiceRoutingRule(Type serviceType)
        {
            return ServiceIdToRoutingRule[GetServiceId(serviceType)];
        }

        public static void SetServiceRoutingRule(string serviceId, RoutingRule serviceMetaInfo)
        {
            ServiceIdToRoutingRule[serviceId] = serviceMetaInfo;
        }
    }
}
