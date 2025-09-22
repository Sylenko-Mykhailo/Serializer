using System.Buffers.Binary;
using System.Text;
using ArrayPoolSerializer.Helper;
using ArrayPoolSerializer.Interfaces;
using SuperSerializer.Helper;

namespace ArrayPoolSerializer.Services;

public class Serializer<T> : ISerializer<T>, IDisposable where T : class
{
    private const int IntSize = 4;
    private readonly DynamicBuffer _buffer;
    private readonly ILogger? _logger;
    private readonly TypeMetadata<T> _metadata = new();
    private bool _disposed;

    public Serializer(ILogger? logger = null)
    {
        _logger = logger;
        _buffer = new DynamicBuffer();
        _buffer.EnsureCapacity(DynamicBuffer.CalculateApproxSize(_metadata));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Serialize(T obj, Stream stream)
    {
        Serialize(new List<T> { obj }, stream);
    }

    public void Serialize(List<T> listToSerialize, Stream stream)
    {
        SaveProperties(stream);

        foreach (var obj in listToSerialize)
        {
            var requiredSize = DynamicBuffer.CalculateApproxSize(obj, _metadata);
            _buffer.EnsureCapacity(requiredSize);
            _buffer.Clear();

            SaveValues(obj, stream);

            _logger?.Log($"Writing for object of type {typeof(T).Name}.");
        }

        _logger?.Log($"Serialization of {listToSerialize.Count} completed.");
    }

    private void SaveProperties(Stream stream)
    {
        var sizeOfAllPropInfo = IntSize;
        var sizesOfProperties = new int[_metadata.PropertiesCount];

        for (var i = 0; i < _metadata.PropertiesCount; i++)
        {
            var propNameBytes = _metadata.Properties[i].NameBytes;
            sizesOfProperties[i] = IntSize + propNameBytes.Length + IntSize;
            sizeOfAllPropInfo += IntSize + sizesOfProperties[i];
        }

        _buffer.EnsureCapacity(sizeOfAllPropInfo);
        _buffer.Clear();

        var span = _buffer.Span[..sizeOfAllPropInfo];
        BinaryPrimitives.WriteInt32LittleEndian(span, _metadata.PropertiesCount);
        span = span[IntSize..];

        for (var i = 0; i < _metadata.PropertiesCount; i++)
        {
            var prop = _metadata.Properties[i];

            BinaryPrimitives.WriteInt32LittleEndian(span, sizesOfProperties[i]);
            span = span[IntSize..];

            var name = prop.Name.AsSpan();
            var nameByteCount = Encoding.UTF8.GetByteCount(name);

            BinaryPrimitives.WriteInt32LittleEndian(span, nameByteCount);
            span = span[IntSize..];

            var written = Encoding.UTF8.GetBytes(name, span);
            span = span[written..];

            BinaryPrimitives.WriteInt32LittleEndian(span, prop.TypeCode);
            span = span[IntSize..];
        }

        _logger?.Log($"Finished writing property metadata, total bytes: {sizeOfAllPropInfo}.");
        stream.Write(_buffer.Span[..sizeOfAllPropInfo]);
    }

    private void SaveValues(T obj, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(obj);

        var sizeOfValuesToWrite = _metadata.GetTotalSize(obj);
        _buffer.EnsureCapacity(sizeOfValuesToWrite);
        _buffer.Clear();

        var span = _buffer.Span[..sizeOfValuesToWrite];

        foreach (var prop in _metadata.Properties)
        {
            var value = prop.Getter(obj);
            prop.Write(span, value);

            var writtenSize = prop.GetValueSize(obj);
            span = span[writtenSize..];

            _logger?.Log($"Serialized property '{prop.Name}'.");
        }

        stream.Write(_buffer.Span[..sizeOfValuesToWrite]);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing) _buffer.Dispose();

            _disposed = true;
        }
    }

    ~Serializer()
    {
        Dispose(false);
    }
}