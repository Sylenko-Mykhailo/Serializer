using ArrayPoolSerializer.ClassesToSerialize;
using ArrayPoolSerializer.Services;

var list = new List<Person>
{
    new(1, "Alice", 30, "dcdqaCDC", 1.23f, double.E, true),
    new(2, "Bob", 25, "qweqwe", 1.25f, double.NaN, false),
    new(2, "Bob", 25, "qweqwe", 1.25f, double.NaN, false),
    new(2, "Bob", 25, "qweqwe", 1.25f, double.NaN, false),
    new(2, "Bob", 25, "qweqwe", 1.25f, double.NaN, false),
    new(2, "Bob", 25, "qweqwe", 1.25f, double.NaN, false),
    new(2, "Bob", 25, "qweqwe", 1.25f, double.NaN, false),
    new(2, "Bob", 25, "qweqwe", 1.25f, double.NaN, false),
    new(2, "Bob", 25, "qweqwe", 1.25f, double.NaN, false),
    new(2, "Bob", 25, "qweqwe", 1.25f, double.NaN, false),
    new(2, "Bob", 25, "qweqwe", 1.25f, double.NaN, false),
    new(2, "Bob", 25, "qweqwe", 1.25f, double.NaN, false),
    new(2, "Bob", 25, "qweqwe", 1.25f, double.NaN, false),
    new(2, "Bob", 25, "qweqwe", 1.25f, double.NaN, false),
    new(2, "Bob", 25, "qweqwe", 1.25f, 1221.111, false)
};

using var stream = new MemoryStream();
using var serializer = new Serializer<Person>();

var p = new Person(1, "Alice", 30, "dcdqaCDC", 1.23f, double.E, true);

serializer.Serialize(list, stream);


stream.Position = 0;

using var deserializer = new Deserializer<Persan>();
var deserializedList = deserializer.Deserialize(stream);

foreach (var person in deserializedList) Console.WriteLine(person.ToString());