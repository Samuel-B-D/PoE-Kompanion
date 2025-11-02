# Changelog

All notable changes to PoE Kompanion will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2025-11-02

### Added
- Support for modifier keys (Ctrl, Shift, Alt) in hotkey combinations
- Hideout macro with `/hideout` command (default: F5)
- Exit to character selection macro with `/exit` command (default: Ctrl+Shift+Space)
- Virtual keyboard implementation using uinput for sending chat commands
- Keyboard layout detection via xmodmap for proper character mapping
- HotkeyCombo class for managing key + modifier combinations
- Deferred execution for hotkeys with modifiers (fires on modifier release)

### Changed
- HotkeyPickerButton now captures and displays full key combinations with modifiers
- Configuration system updated to store HotkeyCombo instead of simple KeyCode
- Background process now receives keyboard layout map at startup

### Technical
- Added KeyboardLayoutHelper for X11 keyboard layout mapping
- Expanded IPC with ChatCommandMessage and KeyboardLayoutMapMessage
- PoETracker creates virtual keyboard device for text input simulation

## [0.1.0] - 2025-11-02

### Added
- Initial release
- Instant logout macro functionality (default: Backtick key)
- Configuration window for customizing hotkeys (default: F10)
- System tray integration with notifications
- Automatic Path of Exile process detection
- TCP connection tracking and termination for instant logout
- AppImage packaging for easy distribution
- IPC communication between foreground and background processes

### Technical
- Built with .NET 9 and Avalonia UI
- Uses SharpHook for global hotkey capture
- Unix domain socket IPC for process communication
- Background process runs with CAP_NET_ADMIN capability for network operations

[0.2.0]: https://github.com/Samuel-B-D/PoE-Kompanion/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/Samuel-B-D/PoE-Kompanion/releases/tag/v0.1.0
