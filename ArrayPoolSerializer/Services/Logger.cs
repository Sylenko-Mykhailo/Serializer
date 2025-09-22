using ArrayPoolSerializer.Interfaces;

namespace ArrayPoolSerializer.Services;

public class Logger : ILogger
{
    public void Log(string message)
    {
        Console.WriteLine($"[Log]: {message}");
    }
}