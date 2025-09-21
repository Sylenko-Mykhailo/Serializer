using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using SuperSerializer.Helper;
using SuperSerializer.Interfaces;

namespace SuperSerializer.Services;

public class Deserializer<T> : IDeserializer<T> where T : class, new()
{
    private const int IntSize = 4;
    private readonly TypeMetadata<T> _metadata = new();
    private readonly ILogger _logger;

    public Deserializer(ILogger logger)
    {
        _logger = logger;
    }

    public List<T> Deserialize(Stream stream)
    {
        var list = new List<T>();
        var propertiesInfo = ReadProperties(stream);

        while (stream.Position < stream.Length)
        {
            var obj = ReadValues(stream, propertiesInfo);
            list.Add(obj);
        }

        return list;
    }

    private List<(string, Type)> ReadProperties(Stream stream)
    {
        var properties = new List<(string Name, Type type)>();

        Span<byte> buffer = stackalloc byte[IntSize];
        stream.ReadExactly(buffer);
        var count = BinaryPrimitives.ReadInt32LittleEndian(buffer);
        _logger.Log($"Reading metadata for {count} properties.");

        for (var i = 0; i < count; i++)
        {
            stream.ReadExactly(buffer);
            var propSize = BinaryPrimitives.ReadInt32LittleEndian(buffer);

            byte[] rented = ArrayPool<byte>.Shared.Rent(propSize);
            try
            {
                var propInfoBuffer = rented.AsSpan(0, propSize);
                stream.ReadExactly(propInfoBuffer);

                var span = propInfoBuffer;

                var nameSize = BinaryPrimitives.ReadInt32LittleEndian(span);
                span = span[IntSize..];

                var nameBytes = span[..nameSize];
                span = span[nameSize..];
                var propName = Encoding.UTF8.GetString(nameBytes);

                var typeCode = BinaryPrimitives.ReadInt32LittleEndian(span);
                var propType = PropertyMetadata<T>.FromTypeCode(typeCode);

                _logger.Log($"Property '{propName}' of type {propType.Name} read from metadata.");
                properties.Add((propName, propType));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        return properties;
    }

    private T ReadValues(Stream stream, List<(string, Type)> propertyNames)
    {
        var obj = new T();

        foreach (var (propName, propType) in propertyNames)
        {
            var meta = _metadata.Properties.FirstOrDefault(p => p.Name == propName);
            if (meta == null || meta.GetType() != propType)
            {
                // Skip unknown property
                Span<byte> skipSizeBuf = stackalloc byte[IntSize];
                stream.ReadExactly(skipSizeBuf);
                var valueSizeToSkip = BinaryPrimitives.ReadInt32LittleEndian(skipSizeBuf);
                stream.Position += valueSizeToSkip;
                _logger.Log($"No corresponding property found {propType.Name} of name {propName}");
                continue;
            }

            Span<byte> sizeBuffer = stackalloc byte[IntSize];
            stream.ReadExactly(sizeBuffer);
            var valueSize = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);

            byte[] rented = ArrayPool<byte>.Shared.Rent(valueSize);
            try
            {
                var valueSpan = rented.AsSpan(0, valueSize);
                stream.ReadExactly(valueSpan);

                var value = meta.Read(valueSpan);
                meta.Setter(obj, value);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        _logger.Log($"Finished deserializing object of type '{typeof(T).Name}'.");
        return obj;
    }
}
