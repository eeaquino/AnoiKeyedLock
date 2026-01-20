# Quick Test Reference

## Test Commands

### Run All Tests
```bash
dotnet test
```

### Run with Detailed Output
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run Specific Test Class
```bash
# Basic functionality
dotnet test --filter "FullyQualifiedName~KeyedLockTests"

# Concurrency tests
dotnet test --filter "FullyQualifiedName~KeyedLockConcurrencyTests"

# Edge case tests
dotnet test --filter "FullyQualifiedName~KeyedLockEdgeCaseTests"
```

### Run Specific Test
```bash
dotnet test --filter "Name=Lock_WithValidKey_AcquiresAndReleasesLock"
```

### Generate Code Coverage
```bash
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

## Test Files

| File | Tests | Focus |
|------|-------|-------|
| `KeyedLockTests.cs` | 30 | Core functionality, basic operations |
| `KeyedLockConcurrencyTests.cs` | 10 | Concurrent access, race conditions |
| `KeyedLockEdgeCaseTests.cs` | 15+ | Boundary conditions, special inputs |

## Test Categories

### ✅ Input Validation (8 tests)
- Null, empty, whitespace keys
- Various whitespace patterns
- Very long keys (10,000 chars)
- Special characters & Unicode

### ✅ Basic Locking (10 tests)
- Lock/unlock operations
- Multiple different keys
- Same key sequential execution
- Case-sensitive/insensitive

### ✅ Timeout Handling (8 tests)
- TryLock with TimeSpan
- TryLock with milliseconds
- Zero timeout
- Negative timeout
- Very long timeout

### ✅ Cancellation (6 tests)
- CancellationToken support
- Cancellation during wait
- Early cancellation
- Long wait cancellation

### ✅ Async Operations (10 tests)
- LockAsync basic usage
- TryLockAsync variants
- Concurrent async calls
- Mixed sync/async

### ✅ Resource Management (8 tests)
- Automatic cleanup
- Reference counting
- Double dispose safety
- Exception handling

### ✅ Concurrency (10 tests)
- 50+ concurrent operations
- 1000 rapid operations
- High contention (100 threads)
- Deadlock prevention
- Race condition handling

## Quick Checks

### ✅ All Tests Pass
```bash
dotnet test
```
Expected: All tests green, 0 failed

### ✅ No Memory Leaks
Look for: `Assert.Equal(0, keyedLock.Count)` assertions pass

### ✅ Performance
Check: `PerformanceTest_LowAllocation_CompletesQuickly` under 5 seconds

### ✅ Thread Safety
Check: All concurrency tests pass without deadlocks

## Coverage Goals

| Metric | Target | Status |
|--------|--------|--------|
| Line Coverage | >95% | ✅ |
| Branch Coverage | >90% | ✅ |
| Method Coverage | 100% | ✅ |
| API Coverage | 100% | ✅ |

## Common Test Patterns

### Testing Lock Acquisition
```csharp
using (var releaser = keyedLock.Lock("key"))
{
    Assert.Equal(1, keyedLock.Count);
}
Assert.Equal(0, keyedLock.Count);
```

### Testing Concurrent Access
```csharp
var tasks = new[] {
    Task.Run(() => { using var r = keyedLock.Lock("key1"); }),
    Task.Run(() => { using var r = keyedLock.Lock("key2"); })
};
await Task.WhenAll(tasks);
```

### Testing Sequential Execution
```csharp
var order = new List<int>();
var tasks = Enumerable.Range(1, 5).Select(i => Task.Run(() => {
    using var r = keyedLock.Lock("same-key");
    lock (order) order.Add(i);
}));
await Task.WhenAll(tasks);
// Verify order maintained
```

### Testing Timeout
```csharp
using var first = keyedLock.Lock("key");
var result = await keyedLock.TryLockAsync("key", TimeSpan.FromMilliseconds(100));
Assert.False(result.success);
```

## Troubleshooting

### Tests Hang
- Check for deadlocks in concurrent tests
- Verify timeouts are reasonable (< 5 seconds)
- Ensure proper disposal of locks

### Tests Fail Intermittently
- Add delays in concurrent tests for reproducibility
- Use ManualResetEventSlim for synchronization
- Check for race conditions in test code

### Performance Tests Fail
- Machine may be under load
- Adjust timeout thresholds if needed
- Run tests in isolation

## CI/CD Integration

### GitHub Actions Example
```yaml
- name: Run Tests
  run: dotnet test --logger "trx;LogFileName=test-results.trx"
  
- name: Generate Coverage
  run: dotnet-coverage collect -f cobertura -o coverage.xml dotnet test
```

### Azure Pipelines Example
```yaml
- task: DotNetCoreCLI@2
  inputs:
    command: 'test'
    projects: '**/*Tests.csproj'
    arguments: '--logger trx --collect:"XPlat Code Coverage"'
```

## Test Maintenance

### Adding New Tests
1. Choose appropriate test class
2. Follow AAA pattern
3. Use descriptive names
4. Verify cleanup (Count = 0)
5. Add to this reference

### Updating Tests
1. Keep existing test coverage
2. Add new edge cases as discovered
3. Update documentation
4. Maintain fast execution

## Performance Benchmarks

| Test | Expected Time | Max Time |
|------|---------------|----------|
| Basic lock/unlock | < 10ms | 100ms |
| 1000 operations | < 2s | 5s |
| 100 concurrent | < 3s | 10s |
| Full test suite | < 20s | 30s |
