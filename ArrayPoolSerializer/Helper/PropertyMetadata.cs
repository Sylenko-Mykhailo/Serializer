using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace SuperSerializer.Helper;

public delegate void WriteDelegate(Span<byte> span, object? value);
public delegate object? ReadDelegate(ReadOnlySpan<byte> span);
public class PropertyMetadata<T>
{
    public string Name { get; }
    
    public byte[] NameBytes { get; }
    public Func<T, object?> Getter { get; }
    public Action<T, object?> Setter { get; }
    public PropertyInfo PropertyInfo { get; }
    public Type PropertyType { get; }
    public int TypeCode { get; }

    public WriteDelegate Write { get; }
    public ReadDelegate Read { get; }

    private const int IntSize = sizeof(int);

    public PropertyMetadata(PropertyInfo prop)
    {
        Name = prop.Name;
        NameBytes = Encoding.UTF8.GetBytes(Name);
        PropertyInfo = prop;
        
        Getter = obj => prop.GetValue(obj);
        Setter = (obj, value) => prop.SetValue(obj, value);
        

        PropertyType = prop.PropertyType;
        TypeCode = GetTypeCode(PropertyType);

        // Assign lambdas for writing and reading
        (Write, Read) = CreateHandlers(PropertyType);
    }

    public static bool IsSupported(Type type) =>
        type == typeof(string) ||
        type == typeof(int) ||
        type == typeof(double) ||
        type == typeof(float) ||
        type == typeof(bool);

    private static int GetTypeCode(Type type) =>
        type == typeof(string) ? 0 :
        type == typeof(int) ? 1 :
        type == typeof(double) ? 2 :
        type == typeof(float) ? 3 :
        type == typeof(bool) ? 4 :
        throw new NotSupportedException($"Type '{type.Name}' is not supported.");

    public static Type FromTypeCode(int code) =>
        code switch
        {
            0 => typeof(string),
            1 => typeof(int),
            2 => typeof(double),
            3 => typeof(float),
            4 => typeof(bool),
            _ => throw new NotSupportedException($"Type code {code} is not supported.")
        };

    public int GetValueSize(object? instance)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }
        var value = PropertyInfo.GetValue(instance);

        return PropertyType switch
        {
            not null when PropertyType == typeof(string) =>
                IntSize + (value is string str ? Encoding.UTF8.GetByteCount(str) : 0),
            not null when PropertyType == typeof(int) => IntSize + sizeof(int),
            not null when PropertyType == typeof(double) => IntSize + sizeof(double),
            not null when PropertyType == typeof(float) => IntSize + sizeof(float),
            not null when PropertyType == typeof(bool) => IntSize + 1,
            _ => 0
        };
    }

    private static (WriteDelegate write, ReadDelegate read) CreateHandlers(Type type)
    {
        if (type == typeof(string))
        {
            return (
                (span, value) =>
                {
                    var strValue = (string?)value ?? string.Empty;
                    int byteCount = Encoding.UTF8.GetByteCount(strValue);
                    BinaryPrimitives.WriteInt32LittleEndian(span, byteCount);
                    span = span[IntSize..];
                    Encoding.UTF8.GetBytes(strValue.AsSpan(), span);
                },
                span => Encoding.UTF8.GetString(span)
            );
        }
        if (type == typeof(int))
        {
            return (
                (span, value) =>
                {
                    BinaryPrimitives.WriteInt32LittleEndian(span, sizeof(int));
                    span = span[IntSize..];
                    BinaryPrimitives.WriteInt32LittleEndian(span, (int)(value ?? 0));
                },
                span => BinaryPrimitives.ReadInt32LittleEndian(span)
            );
        }
        if (type == typeof(double))
        {
            return (
                (span, value) =>
                {
                    BinaryPrimitives.WriteInt32LittleEndian(span, sizeof(double));
                    span = span[IntSize..];
                    BinaryPrimitives.WriteDoubleLittleEndian(span, (double)(value ?? 0.0));
                },
                span => BinaryPrimitives.ReadDoubleLittleEndian(span)
            );
        }
        if (type == typeof(float))
        {
            return (
                (span, value) =>
                {
                    BinaryPrimitives.WriteInt32LittleEndian(span, sizeof(float));
                    span = span[IntSize..];
                    BinaryPrimitives.WriteSingleLittleEndian(span, (float)(value ?? 0.0f));
                },
                span => BinaryPrimitives.ReadSingleLittleEndian(span)
            );
        }
        if (type == typeof(bool))
        {
            return (
                (span, value) =>
                {
                    BinaryPrimitives.WriteInt32LittleEndian(span, 1);
                    span = span[IntSize..];
                    span[0] = (byte)((bool)(value ?? false) ? 1 : 0);
                },
                span => span[0] != 0
            );
        }

        throw new NotSupportedException($"Unsupported type {type.Name}");
    }
}
