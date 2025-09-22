using SerializerLibrary.Helper;

namespace SuperSerializer.Helper;

public class DynamicBuffer
{
    private const int IntSize = sizeof(int);
    
    private byte[] _buffer = new byte[256];
    
    public Span<byte> Span => _buffer.AsSpan();
    
    private const int MaxAllowedSize = 10240;
    

    public void EnsureCapacity(int requiredSize)
    {
        if (requiredSize > MaxAllowedSize)
            throw new InvalidOperationException(
                $"Required buffer size {requiredSize} exceeds maximum allowed {MaxAllowedSize} bytes.");

        if (_buffer.Length < requiredSize)
        {
            var newSize = _buffer.Length;

            while (newSize < requiredSize)
            {
                newSize *= 2;
                if (newSize > MaxAllowedSize)
                {
                    newSize = MaxAllowedSize;
                    break;
                }
            }
            _buffer = new byte[newSize];
        }
    }

    
    public void Clear() => Array.Clear(_buffer, 0, _buffer.Length);

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
