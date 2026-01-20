# AnoiKeyedLock

A high-performance, low-allocation keyed lock implementation for .NET that ensures exclusive access per string key with automatic resource cleanup.

## Features

✅ **High Performance**: Lock-free reference counting with minimal contention  
✅ **Low Allocations**: Struct-based releaser avoids heap allocations  
✅ **Automatic Cleanup**: Keys and semaphores are automatically removed when no longer needed  
✅ **Thread-Safe**: Fully thread-safe for concurrent access  
✅ **Flexible API**: Support for sync/async, timeouts, and cancellation tokens  
✅ **String Keys**: Optimized for string-based locking with optional case-insensitive comparison  
✅ **.NET Standard 2.1**: Compatible with .NET Core 3.0+, .NET 5+, and modern platforms

## Installation

Simply include the `KeyedLock.cs` file in your project, or build and reference the assembly.

## Quick Start

```csharp
using AnoiKeyedLock;

var keyedLock = new KeyedLock();

// Basic usage
using (var releaser = keyedLock.Lock("myKey"))
{
    // Only one thread can execute this block for "myKey" at a time
    DoWork();
}

// Async usage
using (var releaser = await keyedLock.LockAsync("myKey"))
{
    await DoWorkAsync();
}

// With timeout
if (keyedLock.TryLock("myKey", TimeSpan.FromSeconds(5), out var releaser))
{
    using (releaser)
    {
        DoWork();
    }
}
```

## API Reference

### Constructor

```csharp
// Default (ordinal) string comparison
var keyedLock = new KeyedLock();

// Case-insensitive comparison
var keyedLock = new KeyedLock(StringComparer.OrdinalIgnoreCase);
```

### Synchronous Methods

#### `Lock(string key)`
Acquires a lock for the specified key. Blocks indefinitely until the lock is acquired.

```csharp
KeyedLockReleaser Lock(string key)
```

#### `TryLock(string key, TimeSpan timeout, out KeyedLockReleaser releaser)`
Tries to acquire a lock within the specified timeout.

```csharp
bool TryLock(string key, TimeSpan timeout, out KeyedLockReleaser releaser)
bool TryLock(string key, int millisecondsTimeout, out KeyedLockReleaser releaser)
```

#### `TryLock(string key, CancellationToken cancellationToken, out KeyedLockReleaser releaser)`
Tries to acquire a lock with cancellation support.

```csharp
bool TryLock(string key, CancellationToken cancellationToken, out KeyedLockReleaser releaser)
```

### Asynchronous Methods

#### `LockAsync(string key, CancellationToken cancellationToken = default)`
Asynchronously acquires a lock for the specified key.

```csharp
Task<KeyedLockReleaser> LockAsync(string key, CancellationToken cancellationToken = default)
```

#### `TryLockAsync(string key, TimeSpan timeout, CancellationToken cancellationToken = default)`
Tries to asynchronously acquire a lock within the specified timeout.

```csharp
Task<(bool success, KeyedLockReleaser releaser)> TryLockAsync(
    string key, 
    TimeSpan timeout, 
    CancellationToken cancellationToken = default)

Task<(bool success, KeyedLockReleaser releaser)> TryLockAsync(
    string key, 
    int millisecondsTimeout, 
    CancellationToken cancellationToken = default)
```

### Properties

#### `Count`
Gets the current number of keys being tracked.

```csharp
int Count { get; }
```

## How It Works

### Architecture

1. **Concurrent Dictionary**: Stores semaphores keyed by string keys
2. **Reference Counting**: Tracks how many threads are waiting for or holding each lock
3. **Automatic Cleanup**: When the reference count reaches zero, the semaphore is disposed and removed
4. **Struct-based Releaser**: The `KeyedLockReleaser` is a struct to avoid heap allocations

### Thread Safety

- Multiple threads can acquire locks on different keys simultaneously without blocking
- Multiple threads waiting on the same key will queue in FIFO order
- Reference counting uses lock-free atomic operations (Interlocked)
- Dictionary operations use ConcurrentDictionary for thread-safe access

### Performance Characteristics

| Operation | Complexity | Allocations |
|-----------|-----------|-------------|
| Lock acquisition | O(1) amortized | ~0 (struct releaser) |
| Lock release | O(1) amortized | 0 |
| Key cleanup | O(1) amortized | 0 |

## Use Cases

### Prevent Duplicate API Calls
```csharp
public class ApiClient
{
    private readonly KeyedLock _lock = new KeyedLock();

    public async Task<User> GetUserAsync(string userId)
    {
        using (var releaser = await _lock.LockAsync(userId))
        {
            return await _httpClient.GetFromJsonAsync<User>($"/users/{userId}");
        }
    }
}
```

### File Processing Coordination
```csharp
public class FileProcessor
{
    private readonly KeyedLock _lock = new KeyedLock();

    public async Task ProcessFileAsync(string filePath)
    {
        using (var releaser = await _lock.LockAsync(filePath))
        {
            // Ensure only one thread processes this file at a time
            await ProcessFileInternalAsync(filePath);
        }
    }
}
```

### Database Connection Management
```csharp
public class ConnectionManager
{
    private readonly KeyedLock _lock = new KeyedLock();

    public async Task<T> ExecuteAsync<T>(string connectionString, Func<DbConnection, Task<T>> operation)
    {
        using (var releaser = await _lock.LockAsync(connectionString))
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                return await operation(connection);
            }
        }
    }
}
```

### Rate Limiting
```csharp
public class RateLimiter
{
    private readonly KeyedLock _lock = new KeyedLock();

    public async Task<bool> TryExecuteAsync(string clientId, Func<Task> action, TimeSpan minInterval)
    {
        var result = await _lock.TryLockAsync(clientId, TimeSpan.FromMilliseconds(1));
        if (result.success)
        {
            using (result.releaser)
            {
                await action();
                await Task.Delay(minInterval); // Enforce minimum interval
                return true;
            }
        }
        return false;
    }
}
```

## Best Practices

1. ✅ **Always use `using` statements** to ensure locks are released
2. ✅ **Keep critical sections short** to minimize contention
3. ✅ **Use async methods for I/O operations** to avoid blocking threads
4. ✅ **Keys cannot be null or whitespace** - validation is performed automatically
5. ✅ **Handle timeouts gracefully** - always have a fallback strategy
6. ❌ **Avoid nested locks** on the same key (not reentrant)
7. ❌ **Don't hold locks across long-running operations** without timeouts

## Requirements

- .NET Standard 2.1 or higher
- C# 7.3 or higher

## Compatible With

- .NET Core 3.0+
- .NET 5, 6, 7, 8, 9+
- Xamarin (iOS 12.16+, Android 10.0+)
- Unity 2021.2+

## Examples

See `Examples.cs` for comprehensive usage examples including:
- Basic synchronous and asynchronous locking
- Timeout handling
- Cancellation support
- Real-world scenarios (file processing, API calls, etc.)
- Performance testing

## License

[Your License Here]

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues.
