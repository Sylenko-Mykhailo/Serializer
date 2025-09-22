namespace ArrayPoolSerializer.Interfaces;

public interface ISerializer<T>
{
    public void Serialize(T obj, Stream stream);

    public void Serialize(List<T> listToSerialize, Stream stream);
}