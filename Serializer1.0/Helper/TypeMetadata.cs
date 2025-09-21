using System.Reflection;
using System.Text;
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
    
    public static int CalculateApproxSize<T>(T obj, TypeMetadata<T> metadata)
    {
        // Metadata size
        var size = IntSize + (from prop in metadata.Properties
            select Encoding.UTF8.GetBytes(prop.Name)
            into propNameBytes
            select IntSize // size of this property's metadata
                   + IntSize // name length
                   + propNameBytes.Length // actual name bytes
                   + IntSize
            into propInfoSize
            select IntSize + propInfoSize).Sum(); // property count

        // Values size
        size += metadata.GetTotalSize(obj);

        return size;
    }

    private const int IntSize = 4;

    public static int CalculateApproxSize<T>(TypeMetadata<T> metadata)
    {
        return IntSize + (from prop in metadata.Properties
            select Encoding.UTF8.GetBytes(prop.Name)
            into propNameBytes
            select IntSize // size of this property's metadata
                   + IntSize // name length
                   + propNameBytes.Length // actual name bytes
                   + IntSize
            into propInfoSize
            select IntSize + propInfoSize).Sum();
    }
}