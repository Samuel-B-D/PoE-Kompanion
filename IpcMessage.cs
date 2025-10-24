namespace PoEKompanion;

using MessagePack;

[MessagePackObject]
[Union(0, typeof(ForceLogoutMessage))]
[Union(1, typeof(NotificationMessage))]
[Union(2, typeof(SetAlwaysOnTopMessage))]
public abstract record IpcMessage;

[MessagePackObject]
public sealed record ForceLogoutMessage : IpcMessage;

[MessagePackObject]
public sealed record NotificationMessage(
    [property: Key(0)] string Message,
    [property: Key(1)] bool IsError,
    [property: Key(2)] string Title = "PoE Kompanion"
) : IpcMessage;

[MessagePackObject]
public sealed record SetAlwaysOnTopMessage(
    [property: Key(0)] int ProcessId
) : IpcMessage;
