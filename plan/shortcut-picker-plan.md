# Shortcut Picker Implementation Plan

## Overview
Implement a configurable hotkey system to allow users to customize the logout shortcut (currently hardcoded to `VcBackQuote`). This includes adding a configuration UI, persistent storage, and integrating the custom hotkey into the existing hook system.

---

## Current State Analysis

### Existing Architecture
- **Framework**: Avalonia UI (v11.3.7) with .NET 9.0
- **Build**: Native AOT compilation for performance
- **Hotkey System**: SharpHook (v7.0.3) with `EventLoopGlobalHook`
- **Current Hotkey**: `KeyCode.VcBackQuote` (hardcoded at `App.axaml.cs:16`)
- **Process Architecture**:
  - Main UI process with tray icon
  - Background process (started with `--bg`) that performs actual logout actions
  - Communication via stdin/stdout using `DispatchedActions` enum

### Current Components
1. **App.axaml.cs** - Main application with tray icon and global hotkey hook
2. **App.axaml** - Tray icon definition (currently only "Exit" menu item)
3. **Program.cs** - Entry point and process management
4. **PoETracker.cs** - Background process that monitors and disconnects PoE connections
5. **DispatchedActions.cs** - Enum for inter-process communication

---

## Implementation Plan

### 1. Configuration Data Model

**File**: `Config.cs` (new file)

**Purpose**: Define configuration data structure and persistence logic

**Implementation Details**:
- Create a `ConfigurationModel` class with properties:
  - `LogoutHotkey: KeyCode` (default: `KeyCode.VcBackQuote`)
  - Future-proof for additional settings
- Implement `ConfigurationManager` singleton with methods:
  - `Load()` - Read config from disk
  - `Save()` - Write config to disk
  - `GetDefault()` - Return default configuration
- **Storage Location**: `~/.config/poe-kompanion/config.json`
  - Use `System.Text.Json` for serialization (AOT-compatible)
  - Handle missing directory/file gracefully
  - Validate loaded configuration and fall back to defaults if corrupted

**Why JSON in ~/.config instead of embedding in binary?**
- Native AOT compilation makes embedding configuration in the binary extremely complex
- JSON files are human-readable and easily editable
- Standard Linux practice to use `~/.config/` for application settings
- Allows configuration to persist across application updates

**Dependencies**: None (use built-in System.Text.Json)

---

### 2. Configuration Window

**Files**:
- `Views/ConfigurationWindow.axaml` (new file)
- `Views/ConfigurationWindow.axaml.cs` (new file)

**Purpose**: Provide UI for users to configure hotkeys

**Layout Design**:
```
┌─────────────────────────────────────┐
│ PoE Kompanion Configuration         │
├─────────────────────────────────────┤
│                                     │
│  Hotkeys:                           │
│  ┌───────────────────────────────┐  │
│  │ Logout Hotkey:  [VcBackQuote] │  │
│  └───────────────────────────────┘  │
│                                     │
│         [Save]      [Cancel]        │
└─────────────────────────────────────┘
```

**Implementation Details**:
- Window properties:
  - `Width="400"`, `Height="250"`
  - `CanResize="false"`
  - `WindowStartupLocation="CenterScreen"`
  - `ShowInTaskbar="false"`
- Create custom `HotkeyPickerButton` control:
  - Display current hotkey as text (e.g., "BackQuote", "F1")
  - When clicked, enter "listening" mode (show "Press a key...")
  - Capture next key press using SharpHook
  - Update button text with new key
  - Prevent system keys (Escape allows canceling the capture)
- Buttons:
  - **Save**: Validate, save configuration, close window
  - **Cancel**: Discard changes, close window
- Validation: Warn if hotkey conflicts with common system keys

**XAML Structure**:
- Use `StackPanel` for vertical layout
- `TextBlock` for labels
- Custom `HotkeyPickerButton` (or Button with TextBlock)
- `Grid` for button layout at bottom

**Code-behind**:
- Load current configuration in constructor
- Implement hotkey capture logic using temporary SharpHook instance
- Save configuration on "Save" button click
- Close without saving on "Cancel"

---

### 3. Hotkey Picker Control

**Files**:
- `Controls/HotkeyPickerButton.axaml` (new file)
- `Controls/HotkeyPickerButton.axaml.cs` (new file)

**Purpose**: Reusable control for capturing keyboard input

**Behavior**:
1. **Normal State**: Display current key name (e.g., "BackQuote")
2. **Listening State**: Display "Press a key..." and change appearance
3. **Capture State**:
   - Use SharpHook to capture next key press
   - Handle Escape to cancel
   - Validate key (warn about modifier keys, function keys, etc.)
   - Update display and exit listening mode

**Implementation Details**:
- Custom Avalonia control inheriting from `Button`
- Dependency properties:
  - `SelectedKeyCode` (bindable)
  - `IsListening` (for visual state)
- Use `EventLoopGlobalHook` temporarily during capture
  - Start hook on button click
  - Stop hook after key captured or cancelled
  - Properly dispose hook to avoid resource leaks
- Visual states:
  - Normal: Gray background, black text
  - Listening: Blue/highlighted background, "Press a key..." text
  - Hover: Slight highlight

**Styling**: Use Avalonia's style system for visual states

---

### 4. Tray Menu Integration

**File**: `App.axaml` (modify)

**Changes**:
- Add "Configure" menu item above "Exit"
- Wire up click handler to open configuration window

**Updated XAML**:
```xml
<TrayIcon.Menu>
   <NativeMenu>
      <NativeMenuItem Header="Configure" Click="ConfigureAction" />
      <NativeMenuItem Header="Exit" Click="ExitAction" />
   </NativeMenu>
</TrayIcon.Menu>
```

**File**: `App.axaml.cs` (modify)

**Changes**:
- Add `ConfigureAction` event handler
- Instantiate and show `ConfigurationWindow` as dialog
- Reload configuration after window closes (if saved)

---

### 5. Configuration Loading at Startup

**File**: `App.axaml.cs` (modify)

**Changes**:
- Replace hardcoded `HOTKEY` constant with instance field
- Load configuration in `OnFrameworkInitializationCompleted()`
- Apply loaded hotkey to hook initialization
- Fallback to default (`VcBackQuote`) if config load fails

**Modified Code Flow**:
```
OnFrameworkInitializationCompleted()
  ├─ Load configuration (ConfigurationManager.Load())
  ├─ Set hotkey from config
  ├─ Start background process
  └─ Initialize hook with configured hotkey
```

---

### 6. Dynamic Hook Configuration

**File**: `App.axaml.cs` (modify)

**Changes**:
- Modify `InitHook()` to use instance field instead of constant
- Add method `UpdateHotkey(KeyCode newKey)` to allow runtime changes:
  - Dispose existing hook
  - Create new hook with updated key
  - Start new hook
- Call `UpdateHotkey()` when configuration is saved

**Considerations**:
- Properly dispose `EventLoopGlobalHook` before recreating
- Ensure thread safety (hook runs on separate thread)
- Handle potential exceptions during hook recreation

---

## Implementation Order

### Phase 1: Foundation (No UI)
1. Create `Config.cs` with data model and persistence logic
2. Test configuration save/load functionality
3. Modify `App.axaml.cs` to load configuration at startup
4. Verify hotkey still works with loaded configuration

**Goal**: Configuration system works without UI

---

### Phase 2: UI Components
5. Create `HotkeyPickerButton` control
6. Test hotkey capture in isolation
7. Create `ConfigurationWindow` with picker control
8. Wire up Save/Cancel logic

**Goal**: Configuration window functional but not accessible

---

### Phase 3: Integration
9. Add "Configure" menu item to tray icon
10. Wire up tray menu to open configuration window
11. Implement `UpdateHotkey()` for runtime hotkey changes
12. Test end-to-end: Configure → Save → Hotkey works

**Goal**: Fully functional feature

---

### Phase 4: Polish & Testing
13. Add validation and error handling
14. Test edge cases:
    - Missing config file
    - Corrupted JSON
    - Invalid KeyCode values
    - Rapid configuration changes
15. Add user-friendly key name display (convert `VcBackQuote` → "BackQuote")
16. Test with various hotkeys (F1-F12, letter keys, etc.)

**Goal**: Production-ready feature

---

## Technical Considerations

### 1. AOT Compatibility
- **Constraint**: Application uses `PublishAot=true`
- **Impact**: Cannot use reflection-heavy JSON serialization
- **Solution**: Use `System.Text.Json` with source generators
  - Add `[JsonSerializable]` attribute to context class
  - Ensure all types are AOT-compatible

**Example**:
```csharp
[JsonSerializable(typeof(ConfigurationModel))]
internal partial class ConfigJsonContext : JsonSerializerContext { }
```

### 2. SharpHook Library
- **KeyCode Enum**: Use SharpHook's `KeyCode` enum directly
- **Global Hook**: Only one instance should run at a time
- **Disposal**: Critical to dispose hooks properly to avoid resource leaks
- **Thread Safety**: Hook events fire on background thread

### 3. Configuration File Handling
- **Path**: Use `Environment.GetFolderPath(SpecialFolder.UserProfile)` + `.config/poe-kompanion/`
- **Atomicity**: Write to temp file, then move to avoid corruption
- **Permissions**: Ensure directory is created with appropriate permissions

### 4. Window Management (Avalonia)
- **Dialog vs Window**: Use `ShowDialog()` for modal behavior
- **Lifetime**: Configuration window should not affect application lifetime
- **Threading**: Avalonia UI must be accessed from UI thread

### 5. Error Handling
- **Config Load Failure**: Log warning, use defaults
- **Config Save Failure**: Show error dialog to user
- **Invalid Hotkey**: Validate before saving, warn user
- **Hook Recreation**: Handle failures gracefully, keep old hook active

---

## File Structure (After Implementation)

```
PoEKompanion/
├── App.axaml                          [MODIFIED] Add Configure menu item
├── App.axaml.cs                       [MODIFIED] Configuration integration
├── Config.cs                          [NEW] Configuration model & manager
├── Controls/
│   ├── HotkeyPickerButton.axaml       [NEW] Hotkey picker control
│   └── HotkeyPickerButton.axaml.cs    [NEW] Hotkey picker logic
├── Views/
│   ├── ConfigurationWindow.axaml      [NEW] Configuration window UI
│   └── ConfigurationWindow.axaml.cs   [NEW] Configuration window logic
├── Program.cs                         [NO CHANGE]
├── PoETracker.cs                      [NO CHANGE]
├── DispatchedActions.cs               [NO CHANGE]
└── PoEKompanion.csproj                [MODIFY IF NEEDED] Add JsonSerializable
```

**Runtime Created**:
- `~/.config/poe-kompanion/config.json` - User configuration file

---

## Testing Checklist

### Unit Testing
- [ ] Configuration serialization/deserialization
- [ ] Default configuration generation
- [ ] File I/O error handling
- [ ] Invalid JSON handling

### Integration Testing
- [ ] Configuration window opens from tray menu
- [ ] Hotkey capture works correctly
- [ ] Save persists to disk
- [ ] Cancel discards changes
- [ ] Configuration loads at startup
- [ ] Hotkey respects loaded configuration

### User Acceptance Testing
- [ ] Default hotkey (BackQuote) works on fresh install
- [ ] Changing hotkey updates immediately
- [ ] Configuration persists across restarts
- [ ] Multiple rapid configuration changes don't crash
- [ ] Invalid keys are rejected with clear error messages

### Edge Cases
- [ ] Config file doesn't exist (first run)
- [ ] Config directory doesn't exist
- [ ] Config file is corrupted JSON
- [ ] Config file has invalid KeyCode
- [ ] Simultaneous key presses during capture
- [ ] System keys (Ctrl, Alt, etc.) handled appropriately

---

## Future Enhancements (Out of Scope)

1. **Multiple Hotkeys**: Support additional actions with different hotkeys
2. **Hotkey Combinations**: Modifier keys (Ctrl+Alt+K, etc.)
3. **Key Conflict Detection**: Warn about conflicts with system hotkeys
4. **Import/Export Config**: Share configurations between machines
5. **Visual Hotkey Display**: Show current hotkey in tray tooltip
6. **Configuration Profiles**: Quick switching between different setups

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| AOT compilation breaks JSON serialization | High | Use source generators, test early |
| Hook disposal leaks resources | Medium | Implement IDisposable, test thoroughly |
| Configuration corruption | Low | Atomic writes, validation, defaults fallback |
| UI freezes during hotkey capture | Low | Use async/await, timeout on capture |
| Breaking changes in SharpHook API | Low | Pin version, test before upgrade |

---

## Success Criteria

1. ✅ User can open configuration window from tray icon
2. ✅ User can select a custom hotkey using the picker
3. ✅ Configuration is saved to `~/.config/poe-kompanion/config.json`
4. ✅ Configuration is loaded on application startup
5. ✅ Selected hotkey triggers logout action correctly
6. ✅ Default hotkey remains `VcBackQuote` for new installations
7. ✅ No crashes or resource leaks during configuration changes
8. ✅ Application builds successfully with AOT compilation

---

## Estimated Effort

- **Phase 1 (Foundation)**: 2-3 hours
- **Phase 2 (UI Components)**: 3-4 hours
- **Phase 3 (Integration)**: 2 hours
- **Phase 4 (Polish & Testing)**: 2-3 hours

**Total**: 9-12 hours

---

## Notes

- The application uses Native AOT for performance, so avoid reflection-based patterns
- SharpHook provides cross-platform global hotkey support (already in use)
- Avalonia 11.3.7 is the UI framework (already in use)
- The `VcBackQuote` key corresponds to the backtick/grave accent key (`)
- Consider adding tooltips to explain what each hotkey does in the configuration window
