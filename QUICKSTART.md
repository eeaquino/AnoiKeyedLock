# Quick Start Guide - AnoiKeyedLock

Get started with AnoiKeyedLock in under 5 minutes!

## Installation

```bash
dotnet add package AnoiKeyedLock
```

## Basic Example

```csharp
using AnoiKeyedLock;

var keyedLock = new KeyedLock();

// Lock on a key
using (var releaser = keyedLock.Lock("user-123"))
{
    // Only one thread can execute this for "user-123" at a time
    Console.WriteLine("Processing user 123...");
}
// Lock automatically released
```

## Async Example

```csharp
using AnoiKeyedLock;

var keyedLock = new KeyedLock();

// Async lock
using (var releaser = await keyedLock.LockAsync("order-456"))
{
    // Process order 456
    await ProcessOrderAsync("order-456");
}
```

## With Timeout

```csharp
var result = await keyedLock.TryLockAsync("resource-789", TimeSpan.FromSeconds(5));
if (result.success)
{
    using (result.releaser)
    {
        // Got the lock within 5 seconds
        await ProcessResourceAsync("resource-789");
    }
}
else
{
    // Couldn't get lock in time
    Console.WriteLine("Resource is busy, try again later");
}
```

## Dependency Injection

```csharp
// In Startup.cs or Program.cs
services.AddKeyedLock();

// In your service
public class OrderService
{
    private readonly IKeyedLock _keyedLock;
    
    public OrderService(IKeyedLock keyedLock)
    {
        _keyedLock = keyedLock;
    }
    
    public async Task ProcessOrderAsync(string orderId)
    {
        using (var releaser = await _keyedLock.LockAsync(orderId))
        {
            // Ensure only one process handles this order at a time
            await HandleOrderAsync(orderId);
        }
    }
}
```

## Use Cases

### Prevent Duplicate Processing
```csharp
public async Task ProcessPaymentAsync(string paymentId)
{
    using (var releaser = await _keyedLock.LockAsync($"payment-{paymentId}"))
    {
        // Ensure payment isn't processed twice
        await ChargeCustomerAsync(paymentId);
    }
}
```

### Cache Stampede Prevention
```csharp
public async Task<Data> GetCachedDataAsync(string cacheKey)
{
    var cached = await _cache.GetAsync(cacheKey);
    if (cached != null) return cached;
    
    // Prevent multiple threads from fetching the same data
    using (var releaser = await _keyedLock.LockAsync(cacheKey))
    {
        // Check again in case another thread just populated it
        cached = await _cache.GetAsync(cacheKey);
        if (cached != null) return cached;
        
        // Fetch and cache
        var data = await FetchDataAsync();
        await _cache.SetAsync(cacheKey, data);
        return data;
    }
}
```

### File Operation Coordination
```csharp
public async Task WriteToFileAsync(string filePath, string content)
{
    using (var releaser = await _keyedLock.LockAsync(filePath))
    {
        // Ensure only one write to this file at a time
        await File.WriteAllTextAsync(filePath, content);
    }
}
```

## Next Steps

- Read the full [README](README.md) for detailed documentation
- Check out the [GitHub repository](https://github.com/eeaquino/AnoiKeyedLock)
- Report issues or request features on [GitHub Issues](https://github.com/eeaquino/AnoiKeyedLock/issues)

## Performance Tips

1. **Reuse instances**: Create one `KeyedLock` instance and reuse it
2. **Short critical sections**: Keep locked code sections as short as possible
3. **Use async methods**: Prefer `LockAsync` over `Lock` in async contexts
4. **Cleanup is automatic**: Don't worry about memory leaks; unused locks are automatically cleaned up

## Common Pitfalls

❌ **Don't**: Create a new `KeyedLock` for each operation
```csharp
// BAD - Creates a new lock instance each time
using (var releaser = new KeyedLock().Lock("key"))
{
    // This doesn't actually lock across calls!
}
```

✅ **Do**: Reuse the same instance
```csharp
// GOOD - Reuse the same KeyedLock instance
private readonly KeyedLock _keyedLock = new KeyedLock();

public void Process(string key)
{
    using (var releaser = _keyedLock.Lock(key))
    {
        // Properly locked
    }
}
```

## Support

Need help? Open an issue on [GitHub](https://github.com/eeaquino/AnoiKeyedLock/issues)!
