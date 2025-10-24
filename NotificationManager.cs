namespace PoEKompanion;

using System;
using System.Diagnostics;

public static class NotificationManager
{
    public static void SendInfo(string title, string message)
    {
        Send(title, message, "normal");
    }

    public static void SendError(string title, string message)
    {
        Send(title, message, "critical");
    }

    private static void Send(string title, string message, string urgency)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "notify-send",
                    Arguments = $"-u {urgency} \"{title}\" \"{message}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.Start();
        }
        catch (Exception)
        {
        }
    }
}
