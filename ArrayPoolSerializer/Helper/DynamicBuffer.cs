using System.Buffers;
using SuperSerializer.Helper;

namespace ArrayPoolSerializer.Helper;

public class DynamicBuffer : IDisposable
{
    private const int IntSize = sizeof(int);
    private const int MaxAllowedSize = 10240;

    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(256);

    public Span<byte> Span => _buffer.AsSpan();

    public void Dispose()
    {
        if (_buffer != null)
        {
            ArrayPool<byte>.Shared.Return(_buffer, true);
            _buffer = null!;
        }
    }

    public void EnsureCapacity(int requiredSize)
    {
        if (requiredSize > MaxAllowedSize)
            throw new InvalidOperationException(
                $"Required buffer size {requiredSize} exceeds maximum allowed {MaxAllowedSize} bytes.");

        var newBuffer = ArrayPool<byte>.Shared.Rent(requiredSize);

        ArrayPool<byte>.Shared.Return(_buffer, true);
        _buffer = newBuffer;
    }

    public void Clear()
    {
        Array.Clear(_buffer, 0, _buffer.Length);
    }

    public static int CalculateApproxSize<T>(T obj, TypeMetadata<T> metadata)
    {
        // Metadata size
        var size = IntSize + (from prop in metadata.Properties
            select prop.NameBytes
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

    public static int CalculateApproxSize<T>(TypeMetadata<T> metadata)
    {
        return IntSize + (from prop in metadata.Properties
            select prop.NameBytes
            into propNameBytes
            select IntSize // size of this property's metadata
                   + IntSize // name length
                   + propNameBytes.Length // actual name bytes
                   + IntSize
            into propInfoSize
            select IntSize + propInfoSize).Sum();
    }
}