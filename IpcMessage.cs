namespace PoEKompanion;

using MessagePack;

[MessagePackObject]
[Union(0, typeof(ForceLogoutMessage))]
[Union(1, typeof(NotificationMessage))]
public abstract record IpcMessage;

[MessagePackObject]
public sealed record ForceLogoutMessage : IpcMessage;

[MessagePackObject]
public sealed record NotificationMessage([property: Key(0)] string Title, [property: Key(1)] string Message, [property: Key(2)] bool IsError) : IpcMessage;
