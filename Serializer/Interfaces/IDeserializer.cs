namespace SerializatorDeserializator.Interfaces;

public interface IDeserializer<T> where T : new()
{
    public List<T> DeserializeList(Stream stream);
}