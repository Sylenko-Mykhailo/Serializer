using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using SerializatorDeserializator.Interfaces;

namespace SerializatorDeserializator.Services;

public class Deserializer<T>(ILogger logger) : IDeserializer<T> where T : class, new()
{
    private readonly byte[] _dataBuffer = new byte[2048];


    public List<T> DeserializeList(Stream stream)
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

    private List<(string name, Type type)> ReadProperties(Stream stream)
    {
        var properties = new List<(string name, Type type)>();

        var buffer = _dataBuffer.AsSpan(0, 4);
        stream.ReadExactly(buffer);
        var count = BinaryPrimitives.ReadInt32LittleEndian(buffer);
        logger.Log($"Reading metadata for {count} properties.");

        for (var i = 0; i < count; i++)
        {
            stream.ReadExactly(buffer);
            var propSize = BinaryPrimitives.ReadInt32LittleEndian(buffer);
            Array.Clear(_dataBuffer, 0, _dataBuffer.Length);

            var propInfoBuffer = _dataBuffer.AsSpan(0, propSize);
            stream.ReadExactly(propInfoBuffer);

            var offset = 0;

            // Read property name
            var nameSize = BinaryPrimitives.ReadInt32LittleEndian(propInfoBuffer.Slice(offset, 4));
            offset += 4;
            var nameBytes = propInfoBuffer.Slice(offset, nameSize);
            offset += nameSize;
            var propName = Encoding.UTF8.GetString(nameBytes);

            // Read a property type
            var typeCode = BinaryPrimitives.ReadInt32LittleEndian(propInfoBuffer.Slice(offset, 4));
            Array.Clear(_dataBuffer, 0, _dataBuffer.Length);

            Type propType;
            switch (typeCode)
            {
                case 0: propType = typeof(string); break;
                case 1: propType = typeof(int); break;
                case 2: propType = typeof(double); break;
                case 3: propType = typeof(float); break;
                case 4: propType = typeof(bool); break;
                default:
                    logger.Log($"Unsupported type code {typeCode} for property '{propName}', skipping.");
                    throw new NotSupportedException($"Type code {typeCode} is not supported.");
            }

            logger.Log($"Property '{propName}' of type {propType.Name} read from metadata.");
            properties.Add((propName, propType));
        }

        return properties;
    }

    private T ReadValues(Stream fs, List<(string name, Type type)> propertiesInfo)
    {
        var obj = (T)FormatterServices.GetUninitializedObject(typeof(T));
        var type = typeof(T);

        foreach (var propInfo in propertiesInfo)
        {
            var prop = type.GetProperty(propInfo.name, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
            {
                var sizeBufferToSkip = _dataBuffer.AsSpan(0, 4);
                fs.ReadExactly(sizeBufferToSkip);
                var valueSizeToSkip = BinaryPrimitives.ReadInt32LittleEndian(sizeBufferToSkip);
                fs.Position += valueSizeToSkip;
                Array.Clear(_dataBuffer, 0, _dataBuffer.Length);
                logger.Log(
                    $"Property '{propInfo.name}' not found in type '{type.Name}', skipped {valueSizeToSkip} bytes.");
                continue;
            }

            if (propInfo.type != prop.PropertyType &&
                !(propInfo.type.IsAssignableFrom(prop.PropertyType) ||
                  prop.PropertyType.IsAssignableFrom(propInfo.type)))
            {
                var sizeBufferToSkip = _dataBuffer.AsSpan(0, 4);
                fs.ReadExactly(sizeBufferToSkip);
                var valueSizeToSkip = BinaryPrimitives.ReadInt32LittleEndian(sizeBufferToSkip);
                fs.Position += valueSizeToSkip;
                Array.Clear(_dataBuffer, 0, _dataBuffer.Length);
                logger.Log(
                    $"Property type mismatch for '{propInfo.name}' (expected {propInfo.type.Name}, found {prop.PropertyType.Name}), skipped {valueSizeToSkip} bytes.");
                continue;
            }

            var sizeBuffer = _dataBuffer.AsSpan(0, 4);
            fs.ReadExactly(sizeBuffer);
            var valueSize = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
            Array.Clear(_dataBuffer, 0, _dataBuffer.Length);

            object? value = null;

            if (propInfo.type == typeof(string))
            {
                var valueBytes = _dataBuffer.AsSpan(0, valueSize);
                fs.ReadExactly(valueBytes);
                value = Encoding.UTF8.GetString(valueBytes);
                logger.Log($"Deserialized string property '{propInfo.name}' with length {valueSize}.");
                Array.Clear(_dataBuffer, 0, _dataBuffer.Length);
            }
            else if (propInfo.type == typeof(int))
            {
                var valueBuffer = _dataBuffer.AsSpan(0, valueSize);
                fs.ReadExactly(valueBuffer);
                value = BinaryPrimitives.ReadInt32LittleEndian(valueBuffer);
                logger.Log($"Deserialized int property '{propInfo.name}' with value {value}.");
                Array.Clear(_dataBuffer, 0, _dataBuffer.Length);
            }
            else if (propInfo.type == typeof(double))
            {
                var valueBuffer = _dataBuffer.AsSpan(0, valueSize);
                fs.ReadExactly(valueBuffer);
                value = BinaryPrimitives.ReadDoubleLittleEndian(valueBuffer);
                logger.Log($"Deserialized double property '{propInfo.name}' with value {value}.");
                Array.Clear(_dataBuffer, 0, _dataBuffer.Length);
            }
            else if (propInfo.type == typeof(float))
            {
                var valueBuffer = _dataBuffer.AsSpan(0, valueSize);
                fs.ReadExactly(valueBuffer);
                value = BitConverter.ToSingle(valueBuffer);
                logger.Log($"Deserialized float property '{propInfo.name}' with value {value}.");
                Array.Clear(_dataBuffer, 0, _dataBuffer.Length);
            }
            else if (propInfo.type == typeof(bool))
            {
                var valueBuffer = _dataBuffer.AsSpan(0, valueSize);
                fs.ReadExactly(valueBuffer);
                value = BitConverter.ToBoolean(valueBuffer);
                logger.Log($"Deserialized bool property '{propInfo.name}' with value {value}.");
                Array.Clear(_dataBuffer, 0, _dataBuffer.Length);
            }

            prop.SetValue(obj, value);
        }

        logger.Log($"Finished deserializing object of type '{typeof(T).Name}'.");
        return obj;
    }
}