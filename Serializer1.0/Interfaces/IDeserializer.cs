namespace SuperSerializer.Interfaces;

public interface IDeserializer<T> where T : new()
{
    public List<T> Deserialize(Stream stream);
}