using SuperSerializer.Interfaces;

namespace SuperSerializer.Services;

public class Logger : ILogger
{
    public void Log(string message)
    {
        Console.WriteLine($"[Log]: {message}");
    }
}