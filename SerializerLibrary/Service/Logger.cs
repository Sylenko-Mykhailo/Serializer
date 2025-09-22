using SerializerLibrary.Interfaces;

namespace SerializerLibrary.Service;

public class Logger : ILogger
{
    public void Log(string message)
    {
        Console.WriteLine($"[Log]: {message}");
    }
}