using System.Reflection;
using SuperSerializer.Attributes;

namespace SuperSerializer.Helper;

public class TypeMetadata<T>
{
    public List<PropertyMetadata<T>> Properties { get; }
    
    public int PropertiesCount { get; }

    public TypeMetadata()
    {
        Properties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<SerializeProperty>() != null)
            .Where(p => PropertyMetadata<T>.IsSupported(p.PropertyType))
            .Where(p => p is { CanRead: true, CanWrite: true })
            .Select(p => new PropertyMetadata<T>(p))
            .ToList();
        PropertiesCount = Properties.Count;
    }

    public int GetTotalSize(T obj) =>
        Properties.Sum(p => p.GetValueSize(obj));
}