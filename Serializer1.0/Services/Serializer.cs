using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using SuperSerializer.Exception;
using SuperSerializer.Helper;
using SuperSerializer.Interfaces;

namespace SuperSerializer.Services;

public class Serializer<T> : ISerializer<T> where T : class
{
    private const int IntSize = 4;
    private readonly TypeMetadata<T> _metadata = new();
    private readonly ILogger _logger;

    public Serializer(ILogger logger)
    {
        _logger = logger;
    }

    public void Serialize(T obj, Stream stream)
    {
        Serialize([obj], stream);
    }

    public void Serialize(List<T> listToSerialize, Stream stream)
    {
        SaveProperties(stream);

        foreach (var obj in listToSerialize)
        {
            var requiredSize = TypeMetadata<T>.CalculateApproxSize(obj, _metadata);

            byte[] rented = ArrayPool<byte>.Shared.Rent(requiredSize);
            try
            {
                var span = rented.AsSpan(0, requiredSize);
                span.Clear();

                SaveValues(obj, span, stream);
                _logger.Log($"Writing for object of type {typeof(T).Name}.");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        _logger.Log($"Serialization of {listToSerialize.Count} completed.");
    }

    private void SaveProperties(Stream stream)
    {
        var sizeOfAllPropInfo = IntSize; // property count
        var sizesOfProperties = new int[_metadata.PropertiesCount];

        for (int i = 0; i < _metadata.PropertiesCount; i++)
        {
            var propNameBytes = Encoding.UTF8.GetBytes(_metadata.Properties[i].Name);
            var sizeOfProperty = IntSize  // size of name
                                 + propNameBytes.Length // name
                                 + IntSize; // typeCode
            sizesOfProperties[i] = sizeOfProperty;
            sizeOfAllPropInfo += IntSize // size of whole property
                                 + sizeOfProperty;
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent(sizeOfAllPropInfo);
        try
        {
            var span = rented.AsSpan(0, sizeOfAllPropInfo);
            span.Clear();

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

            _logger.Log($"Finished writing property metadata, total bytes: {sizeOfAllPropInfo}.");
            stream.Write(rented, 0, sizeOfAllPropInfo);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private void SaveValues(T obj, Span<byte> buffer, Stream stream)
    {
        if (obj == null)
            throw new NullNotSupportedException("No null is allowed");

        var span = buffer;

        foreach (var prop in _metadata.Properties)
        {
            var value = prop.Getter(obj);
            prop.Write(span, value);

            var writtenSize = prop.GetValueSize(obj);
            span = span[writtenSize..];

            _logger.Log($"Serialized property '{prop.Name}'.");
        }

        var bytesToWrite = buffer.Length - span.Length;
        stream.Write(buffer.Slice(0, bytesToWrite));
    }
}
