using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using SerializerLibrary.Attributes;
using SerializerLibrary.Interfaces;
using Serialize = Serializer.Interfaces;

namespace Serializer.Services;

public class Serializer<T>(ILogger logger) : Serialize.ISerializer<T>
{
    private readonly byte[] _dataBuffer = new byte[2048];


    public void SerializeList(List<T> listToSerialize, Stream stream)
    {
        var propertiesInfo = SaveProperties();
        
        stream.Write(propertiesInfo);
        
        logger.Log($"Writing {propertiesInfo.Length} bytes of property metadata.");
        Array.Clear(_dataBuffer, 0, _dataBuffer.Length);
        foreach (var obj in listToSerialize)
        {
            var buffer = SaveValues(obj);
            logger.Log($"Writing {buffer.Length} bytes for object of type {typeof(T).Name}.");
            stream.Write(buffer);
            Array.Clear(_dataBuffer, 0, _dataBuffer.Length);
        }


        logger.Log($"Serialization of {listToSerialize.Count} completed.");
    }


    private Span<byte> SaveValues(T obj)
    {
        var sizeOfValuesToWrite = 0;
        var type = typeof(T);

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var propsToSerialize = props
            .Where(p => p.GetCustomAttribute<SerializeProperty>() != null)
            .ToList();

        // Calculate total size
        foreach (var prop in propsToSerialize)
        {
            var propType = prop.PropertyType;
            object? value;

            try
            {
                value = prop.GetValue(obj);
            }
            catch
            {
                value = null;
            }

            if (propType == typeof(string))
            {
                sizeOfValuesToWrite += 4; // for length prefix
                sizeOfValuesToWrite += (string?)value != null
                    ? Encoding.UTF8.GetByteCount((string)value)
                    : 0;
            }
            else if (propType == typeof(int))
            {
                sizeOfValuesToWrite += 4 + 4;
            }
            else if (propType == typeof(double))
            {
                sizeOfValuesToWrite += 4 + 8;
            }
            else if (propType == typeof(float))
            {
                sizeOfValuesToWrite += 4 + 4;
            }
            else if (propType == typeof(bool))
            {
                sizeOfValuesToWrite += 4 + 1;
            }
            else // Unsupported type -> ignore
            {
                logger.Log(
                    $"Property '{prop.Name}' of type '{propType.Name}' is not supported for serialization and will be ignored.");
            }
        }

        var span = _dataBuffer.AsSpan()[..sizeOfValuesToWrite];
        var offset = 0;

        // Write property values
        foreach (var prop in propsToSerialize)
        {
            var propType = prop.PropertyType;
            object? value;
            try
            {
                value = prop.GetValue(obj);
            }
            catch
            {
                value = null;
            }

            if (propType == typeof(string))
            {
                var strValue = (string?)value ?? "";
                var strBytes = Encoding.UTF8.GetBytes(strValue);
                BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), strBytes.Length);
                offset += 4;

                strBytes.CopyTo(span.Slice(offset, strBytes.Length));
                offset += strBytes.Length;
            }
            else if (propType == typeof(int))
            {
                BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), 4);
                offset += 4;
                BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), (int)(value ?? 0));
                offset += 4;
            }
            else if (propType == typeof(double))
            {
                BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), 8);
                offset += 4;
                BinaryPrimitives.WriteDoubleLittleEndian(span.Slice(offset, 8), (double)(value ?? 0.0));
                offset += 8;
            }
            else if (propType == typeof(float))
            {
                BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), 4);
                offset += 4;
                BinaryPrimitives.WriteSingleLittleEndian(span.Slice(offset, 4), (float)(value ?? 0.0f));
                offset += 4;
            }
            else if (propType == typeof(bool))
            {
                BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), 1);
                offset += 4;
                span[offset] = (byte)((bool)(value ?? false) ? 1 : 0);
                offset += 1;
            }
            else
            {
                logger.Log($"Skipping unsupported property '{prop.Name}' of type {propType.Name}.");

                continue;
            } // Unsupported type -> ignore

            logger.Log($"Serialized property '{prop.Name}'.");
        }

        return span;
    }


    private Span<byte> SaveProperties()
    {
        var type = typeof(T);
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Only include supported types with [SerializeProperty] attribute
        var propsToSerialize = props
            .Where(p =>
            {
                var hasAttr = p.GetCustomAttribute<SerializeProperty>() != null;
                var supported = p.PropertyType == typeof(string)
                                || p.PropertyType == typeof(int)
                                || p.PropertyType == typeof(double)
                                || p.PropertyType == typeof(float)
                                || p.PropertyType == typeof(bool);
                if (!supported && hasAttr)
                    logger.Log($"Skipping unsupported property '{p.Name}' of type {p.PropertyType.Name} in metadata.");
                return hasAttr && supported;
            })
            .ToList();

        logger.Log($"Serializing {propsToSerialize.Count} properties for type {typeof(T).Name}.");

        var propInfoSizeToWrite = 4; // for property count

        var sizesOfPropertyInfo = propsToSerialize.Count * 4; // for size of each property info
        propInfoSizeToWrite += sizesOfPropertyInfo;

        var sizes = new int[propsToSerialize.Count];
        var propCounter = 0;

        // Calculate sizes
        foreach (var prop in propsToSerialize)
        {
            var sizeOfProperty = 0;

            var propName = prop.Name;
            var propNameBytes = Encoding.UTF8.GetBytes(propName);

            sizeOfProperty += 4; // for length of name
            sizeOfProperty += propNameBytes.Length; // for name bytes
            sizeOfProperty += 4; // for type code

            sizes[propCounter++] = sizeOfProperty;
            propInfoSizeToWrite += sizeOfProperty;
        }

        var span = _dataBuffer.AsSpan()[..propInfoSizeToWrite];
        var offset = 0;

        // Write property count
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), propsToSerialize.Count);
        offset += 4;

        propCounter = 0;
        foreach (var prop in propsToSerialize)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), sizes[propCounter++]);
            offset += 4;

            var propName = prop.Name;
            var propNameBytes = Encoding.UTF8.GetBytes(propName);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), propNameBytes.Length);
            offset += 4;
            propNameBytes.CopyTo(span.Slice(offset, propNameBytes.Length));
            offset += propNameBytes.Length;

            // Write type code
            int typeName;
            if (prop.PropertyType == typeof(string))
                typeName = 0;
            else if (prop.PropertyType == typeof(int))
                typeName = 1;
            else if (prop.PropertyType == typeof(double))
                typeName = 2;
            else if (prop.PropertyType == typeof(float))
                typeName = 3;
            else //bool
                typeName = 4;

            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), typeName);
            offset += 4;
        }

        logger.Log($"Finished writing property metadata, total bytes: {propInfoSizeToWrite}.");

        return span;
    }
}