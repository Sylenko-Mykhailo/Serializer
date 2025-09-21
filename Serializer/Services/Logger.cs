using SerializatorDeserializator.Interfaces;

namespace SerializatorDeserializator.Services;

public class Logger : ILogger
{
    public void Log(string message)
    {
        Console.WriteLine($"[Log]: {message}");
    }
}