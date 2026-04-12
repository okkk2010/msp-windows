using System;

public static class ErrorLogger
{
    public static event Action<string> OnErrorLogged;

    public static void LogError(string errorCode, string message)
    {
        string logMessage = $"[{errorCode}] {message}";
        OnErrorLogged?.Invoke(logMessage);
    }
}
