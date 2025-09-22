using System.Buffers.Binary;
using System.Text;
using ArrayPoolSerializer.Helper;
using SerializerLibrary.Helper;
using SerializerLibrary.Interfaces;

namespace ArrayPoolSerializer.Services;

public class Deserializer<T> : IDeserializer<T>, IDisposable where T : class, new()
{
    private const int IntSize = 4;
    private readonly DynamicBuffer _buffer;
    private readonly ILogger? _logger;
    private readonly TypeMetadata<T> _metadata = new();
    private bool _disposed;

    public Deserializer(ILogger? logger = null)
    {
        _logger = logger;
        _buffer = new DynamicBuffer();
        _buffer.EnsureCapacity(DynamicBuffer.CalculateApproxSize(_metadata));
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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private List<(string, Type)> ReadProperties(Stream stream)
    {
        var properties = new List<( string Name, Type type)>();

        var buffer = _buffer.Span[..IntSize];
        stream.ReadExactly(buffer);
        var count = BinaryPrimitives.ReadInt32LittleEndian(buffer);
        _logger?.Log($"Reading metadata for {count} properties.");

        for (var i = 0; i < count; i++)
        {
            try
            {
                stream.ReadExactly(buffer);
            }
            catch (EndOfStreamException)
            {
                throw new InvalidOperationException("Unexpected end of stream while reading property metadata.");
            }

            var propSize = BinaryPrimitives.ReadInt32LittleEndian(buffer);

            _buffer.EnsureCapacity(propSize);
            _buffer.Clear();

            var propInfoBuffer = _buffer.Span[..propSize];
            try
            {
                stream.ReadExactly(propInfoBuffer);
            }
            catch (EndOfStreamException)
            {
                throw new InvalidOperationException("Unexpected end of stream while reading property metadata.");
            }

            var span = propInfoBuffer;

            var nameSize = BinaryPrimitives.ReadInt32LittleEndian(span);
            span = span[IntSize..];

            var nameBytes = span[..nameSize];
            span = span[nameSize..];
            var propName = Encoding.UTF8.GetString(nameBytes);

            var typeCode = BinaryPrimitives.ReadInt32LittleEndian(span);
            var propType = PropertyMetadata<T>.FromTypeCode(typeCode);

            _logger?.Log($"Property '{propName}' of type {propType.Name} read from metadata.");
            properties.Add((propName, propType));
        }

        return properties;
    }

    private T ReadValues(Stream stream, List<(string, Type)> propertyNames)
    {
        var obj = new T();

        foreach (var (propName, propType) in propertyNames)
        {
            if (_metadata.Properties.All(p => p.Name != propName)
                || _metadata.Properties.FirstOrDefault(p => p.Name == propName)?.GetType() == propType)
            {
                // Skip unknown property
                var skipSizeBuf = _buffer.Span[..IntSize];
                stream.ReadExactly(skipSizeBuf);
                var valueSizeToSkip = BinaryPrimitives.ReadInt32LittleEndian(skipSizeBuf);
                stream.Position += valueSizeToSkip;
                _logger?.Log($"No corresponding property found ${propType.Name} of name ${propName}");
                continue;
            }

            var sizeBuffer = _buffer.Span[..IntSize];
            stream.ReadExactly(sizeBuffer);
            var valueSize = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);

            _buffer.EnsureCapacity(valueSize);
            _buffer.Clear();

            var valueSpan = _buffer.Span[..valueSize];
            stream.ReadExactly(valueSpan);

            var meta = _metadata.Properties.First(p => p.Name == propName);
            var value = meta.Read(valueSpan);

            var prop = _metadata.Properties.First(p => p.Name == propName);
            prop.Setter(obj, value);
        }

        _logger?.Log($"Finished deserializing object of type '{typeof(T).Name}'.");
        return obj;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing) _buffer.Dispose();

            _disposed = true;
        }
    }

    ~Deserializer()
    {
        Dispose(false);
    }
}