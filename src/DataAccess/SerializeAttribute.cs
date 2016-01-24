using System;

namespace DataAccess
{
    // attribute models that needs to be persistent
    public class PersistenceAttribute : Attribute
    {
        public string DbTableName { get; set; }
    }

    // attribute models that needs to be cached
    public class MainCacheAttribute : Attribute
    {
        public string CacheName { get; set; }
    }

    // attribute fields of models which is the key
    public class KeyAttribute : Attribute
    {
    }

    // attribute non-trivial fields of models
    public class FieldAttribute : Attribute
    {
    }

    // attribute models that needs to be generated non-trivial serializing code
    public class SerializeAttribute : Attribute
    {
    }

    // attribute fields that needs to be detected by AltSerialize
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class FieldIndexAttribute : Attribute
    {
        public byte Index { get; set; }
    }
}
