namespace PoEKompanion;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using SharpHook.Data;

public sealed class HotkeyCombo : IEquatable<HotkeyCombo>
{
    [JsonPropertyName("key")]
    public KeyCode Key { get; init; }

    [JsonPropertyName("ctrl")]
    public bool Ctrl { get; init; }

    [JsonPropertyName("shift")]
    public bool Shift { get; init; }

    [JsonPropertyName("alt")]
    public bool Alt { get; init; }

    [JsonConstructor]
    public HotkeyCombo(KeyCode key, bool ctrl = false, bool shift = false, bool alt = false)
    {
        this.Key = key;
        this.Ctrl = ctrl;
        this.Shift = shift;
        this.Alt = alt;
    }

    public static HotkeyCombo FromKeyCode(KeyCode keyCode) => new(keyCode);

    public bool Matches(KeyCode keyCode, bool ctrl, bool shift, bool alt) =>
        this.Key == keyCode && this.Ctrl == ctrl && this.Shift == shift && this.Alt == alt;

    public bool HasModifiers() => this.Ctrl || this.Shift || this.Alt;

    public override string ToString()
    {
        var parts = new List<string>();

        if (this.Ctrl) parts.Add("Ctrl");
        if (this.Shift) parts.Add("Shift");
        if (this.Alt) parts.Add("Alt");

        var keyName = this.Key.ToString();
        keyName = keyName.StartsWith("Vc") ? keyName[2..] : keyName;
        parts.Add(keyName);

        return string.Join("+", parts);
    }

    public bool Equals(HotkeyCombo? other)
    {
        if (other is null) return false;
        return this.Key == other.Key &&
               this.Ctrl == other.Ctrl &&
               this.Shift == other.Shift &&
               this.Alt == other.Alt;
    }

    public override bool Equals(object? obj) => obj is HotkeyCombo other && this.Equals(other);

    public override int GetHashCode() => HashCode.Combine(this.Key, this.Ctrl, this.Shift, this.Alt);

    public static bool operator ==(HotkeyCombo? left, HotkeyCombo? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(HotkeyCombo? left, HotkeyCombo? right) => !(left == right);
}
