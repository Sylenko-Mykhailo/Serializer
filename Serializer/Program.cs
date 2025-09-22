using Serializer.Services;
using SerializerLibrary.ClassesToSerialize;
using SerializerLibrary.Service;

var list = new List<Person>
{
    new(1, "Alice", 30, "dcdqaCDC", float.Floor(1.23f), double.E, true),
    new(2, "Bob", 25, "qweqwe", 1.25f, double.NaN, false)
};

using var stream = new MemoryStream();
var serializer = new Serializer<Person>(new Logger());
stream.Position = 0;
serializer.SerializeList(list, stream);

var deserializer = new Deserializer<Person>(new Logger());
stream.Position = 0;
var deserializedList = deserializer.DeserializeList(stream);

foreach (var person in deserializedList) Console.WriteLine(person.ToString());