namespace Serializer.Interfaces;

public interface ISerializer<T>
{
    public void SerializeList(List<T> listToSerialize, Stream stream);
}