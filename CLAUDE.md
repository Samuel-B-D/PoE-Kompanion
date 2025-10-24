# Code Guidelines for PoEKompanion

## Git Workflow

- **Local commits allowed**: Create commits, branches, merges, etc. as needed
- **NEVER push**: Do not push to remote repositories under any circumstances
- The user handles all remote operations manually

## General Style

- **Prefer `var`**: Use `var` for local variable declarations instead of explicit types
- **Guard statements**: Single-line conditions that return should be on one line without curly braces
  ```csharp
  // Good
  if (value is null) return;

  // Bad
  if (value is null)
  {
      return;
  }
  ```

## Documentation

- **No doc comments** unless specifically requested
- **Minimal comments**: Only comment non-trivial logic
- Code should be self-explanatory through clear naming

## Asynchronous Programming

- **Prefer async I/O**: Use async/await for all I/O operations whenever possible
  - Use `File.ReadAllTextAsync()` / `File.WriteAllTextAsync()` instead of synchronous variants
  - Use `JsonSerializer.SerializeAsync()` / `JsonSerializer.DeserializeAsync()` instead of synchronous variants
  - Use async Stream-based APIs whenever available
- **Parallelization**: Use parallelization when it's trivial to implement
- **IAsyncEnumerable**: Use `IAsyncEnumerable<T>` where it makes sense for streaming data

## Examples

```csharp
// Good
var config = await LoadConfigAsync();
if (config is null) return default;

// Bad
ConfigurationModel config = LoadConfig();
if (config is null)
{
    return default;
}
```
