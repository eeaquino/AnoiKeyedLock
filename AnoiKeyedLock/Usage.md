# KeyedLock Usage Guide

## Overview

`KeyedLock` is a high-performance, low-allocation synchronization primitive that ensures exclusive access per string key. It automatically cleans up resources when locks are released.

## Features

- **Thread-safe**: Multiple threads can safely acquire locks on different keys concurrently
- **Low allocation**: Uses structs for lock releasers to avoid heap allocations
- **Automatic cleanup**: Keys and semaphores are automatically removed when no longer in use
- **Flexible waiting**: Support for indefinite blocking, timeouts, and cancellation tokens
- **Async support**: Full async/await support with `LockAsync` and `TryLockAsync`
- **String keys**: Optimized for string-based locking with configurable comparison

## Basic Usage

### Synchronous Lock

```csharp
var keyedLock = new KeyedLock();

// Acquire a lock (blocks until available)
using (var releaser = keyedLock.Lock("myKey"))
{
    // Critical section - only one thread can execute this for "myKey" at a time
    Console.WriteLine("Doing work with myKey");
}
// Lock is automatically released when disposed
```

### Async Lock

```csharp
var keyedLock = new KeyedLock();

// Acquire a lock asynchronously
using (var releaser = await keyedLock.LockAsync("myKey"))
{
    // Critical section
    await DoSomeWorkAsync();
}
```

### Try Lock with Timeout

```csharp
var keyedLock = new KeyedLock();

// Try to acquire lock with a timeout
if (keyedLock.TryLock("myKey", TimeSpan.FromSeconds(5), out var releaser))
{
    using (releaser)
    {
        // Got the lock within 5 seconds
        Console.WriteLine("Lock acquired");
    }
}
else
{
    // Timeout - couldn't acquire lock
    Console.WriteLine("Could not acquire lock within timeout");
}
```

### Try Lock with Cancellation Token

```csharp
var keyedLock = new KeyedLock();
var cts = new CancellationTokenSource();

if (keyedLock.TryLock("myKey", cts.Token, out var releaser))
{
    using (releaser)
    {
        // Got the lock
        Console.WriteLine("Lock acquired");
    }
}
else
{
    // Cancelled
    Console.WriteLine("Lock acquisition was cancelled");
}
```

### Async Try Lock with Timeout

```csharp
var keyedLock = new KeyedLock();

var result = await keyedLock.TryLockAsync("myKey", TimeSpan.FromSeconds(5));
if (result.success)
{
    using (result.releaser)
    {
        // Got the lock within 5 seconds
        await DoSomeWorkAsync();
    }
}
else
{
    Console.WriteLine("Could not acquire lock within timeout");
}
```

## Advanced Scenarios

### Case-Insensitive String Comparison

```csharp
// Case-insensitive string comparison
var keyedLock = new KeyedLock(StringComparer.OrdinalIgnoreCase);

using (var releaser = keyedLock.Lock("MyKey"))
{
    // "MyKey", "mykey", "MYKEY" all refer to the same lock
}
```

### Preventing Concurrent API Calls

```csharp
public class ApiClient
{
    private readonly KeyedLock _keyedLock = new KeyedLock();

    public async Task<Data> GetUserDataAsync(string userId)
    {
        // Ensure only one request per userId at a time
        using (var releaser = await _keyedLock.LockAsync(userId))
        {
            return await FetchFromApiAsync(userId);
        }
    }
}
```

### Rate Limiting Per User

```csharp
public class RateLimiter
{
    private readonly KeyedLock _keyedLock = new KeyedLock();

    public async Task<bool> TryExecuteAsync(string userId, Func<Task> action)
    {
        // Try to acquire lock with timeout (rate limit)
        var result = await _keyedLock.TryLockAsync(userId, TimeSpan.FromMilliseconds(100));
        if (result.success)
        {
            using (result.releaser)
            {
                await action();
                await Task.Delay(1000); // Enforce minimum 1 second between calls
                return true;
            }
        }
        return false; // Rate limited
    }
}
```

### Database Connection Pooling

```csharp
public class ConnectionManager
{
    private readonly KeyedLock<string> _keyedLock = new KeyedLock<string>();

    public async Task ExecuteQueryAsync(string connectionString, string query)
    {
        // Ensure only one operation per connection at a time
        using (var releaser = await _keyedLock.LockAsync(connectionString))
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                // Execute query...
### Database Connection Pooling

```csharp
public class ConnectionManager
{
    private readonly KeyedLock _keyedLock = new KeyedLock();

    public async Task ExecuteQueryAsync(string connectionString, string query)
    {
        // Ensure only one operation per connection at a time
        using (var releaser = await _keyedLock.LockAsync(connectionString))
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                // Execute query...
            }
        }
    }
}
```

## Performance Characteristics

- **O(1)** lock acquisition and release (amortized)
- **Zero heap allocations** for the releaser (it's a struct)
- **Automatic cleanup** - no memory leaks from abandoned keys
- **Lock-free reference counting** using atomic operations
- **Minimal contention** - locks are independent per key

## Thread Safety

`KeyedLock` is fully thread-safe. Multiple threads can:
- Acquire locks on different keys simultaneously without blocking each other
- Wait for locks on the same key (first one in gets it, others wait)
- Release locks independently

## Best Practices

1. **Always use `using` statements** to ensure locks are released
2. **Keep critical sections short** to minimize contention
3. **Keys cannot be null or whitespace** - validation is performed automatically
4. **Use async methods** when performing I/O operations inside locks
5. **Handle timeouts gracefully** - have a fallback strategy
6. **Avoid nested locks** on the same key to prevent potential issues

## Notes

- The lock is **not reentrant** - the same thread cannot acquire the same key twice
- Keys are automatically removed from internal storage when the last lock is released
- The struct-based releaser ensures minimal GC pressure
- Compatible with .NET Standard 2.1 and higher
