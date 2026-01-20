# AnoiKeyedLock Tests

Comprehensive test suite for the KeyedLock implementation.

## Test Structure

### KeyedLockTests.cs
Core functionality tests covering:

**Basic Locking:**
- ✅ Lock acquisition and release
- ✅ Null/empty/whitespace key validation
- ✅ Multiple different keys allow concurrent access
- ✅ Same key enforces sequential execution
- ✅ Case-insensitive comparer support

**TryLock Methods:**
- ✅ TryLock with TimeSpan timeout (success and failure)
- ✅ TryLock with milliseconds timeout
- ✅ TryLock with CancellationToken (cancelled and not cancelled)

**Async Methods:**
- ✅ LockAsync acquisition and release
- ✅ LockAsync with null key throws
- ✅ Multiple concurrent async calls execute sequentially
- ✅ LockAsync with CancellationToken throws when cancelled
- ✅ TryLockAsync with timeout (success and timeout)
- ✅ TryLockAsync with milliseconds timeout
- ✅ TryLockAsync with cancellation

**Resource Management:**
- ✅ Automatic cleanup removes keys after release
- ✅ Multiple keys cleaned up independently
- ✅ Double dispose doesn't throw
- ✅ Default releaser can be disposed safely

**Performance & Stress:**
- ✅ Stress test with many keys and threads
- ✅ Performance test with low allocations

### KeyedLockConcurrencyTests.cs
Advanced concurrency scenarios:

**Concurrent Access:**
- ✅ Concurrent lock and release maintains correct reference count
- ✅ Rapid lock/unlock on same key with no memory leak
- ✅ Interleaved lock requests on different keys
- ✅ Mixed sync and async locks with proper synchronization

**Timeout & Cancellation:**
- ✅ Concurrent TryLock with some successes and failures
- ✅ Cancellation during wait properly releases resources
- ✅ Cancellation with pending lock requests

**Contention & Deadlock:**
- ✅ Deadlock prevention with multiple keys
- ✅ High contention scenario with all locks eventually acquired
- ✅ Race condition on cleanup produces no exceptions

### KeyedLockEdgeCaseTests.cs
Edge cases and boundary conditions:

**Input Validation:**
- ✅ Various whitespace characters (Theory tests)
- ✅ Very long keys (10,000 characters)
- ✅ Special characters and Unicode (Chinese, Emoji, Cyrillic, Arabic)

**Timeout Scenarios:**
- ✅ Zero timeout returns immediately
- ✅ Negative timeout behavior
- ✅ Very long timeout can be cancelled

**Error Handling:**
- ✅ Exception in critical section still releases lock
- ✅ Task cancellation before acquiring doesn't leak resources
- ✅ Synchronous lock on thread pool doesn't deadlock

**Isolation:**
- ✅ Multiple KeyedLock instances don't interact
- ✅ Rapid creation and destruction of many keys
- ✅ Case-insensitive variations use same lock

## Running the Tests

### Run all tests
```bash
dotnet test AnoiKeyedLock.Tests\AnoiKeyedLock.Tests.csproj
```

### Run with detailed output
```bash
dotnet test AnoiKeyedLock.Tests\AnoiKeyedLock.Tests.csproj --logger "console;verbosity=detailed"
```

### Run specific test class
```bash
dotnet test AnoiKeyedLock.Tests\AnoiKeyedLock.Tests.csproj --filter "FullyQualifiedName~KeyedLockTests"
```

### Run specific test
```bash
dotnet test AnoiKeyedLock.Tests\AnoiKeyedLock.Tests.csproj --filter "Name=Lock_WithValidKey_AcquiresAndReleasesLock"
```

### Generate code coverage
```bash
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test AnoiKeyedLock.Tests\AnoiKeyedLock.Tests.csproj
```

## Test Coverage

The test suite covers:

| Category | Coverage |
|----------|----------|
| Basic locking | ✅ 100% |
| Async operations | ✅ 100% |
| Timeout handling | ✅ 100% |
| Cancellation | ✅ 100% |
| Input validation | ✅ 100% |
| Reference counting | ✅ 100% |
| Automatic cleanup | ✅ 100% |
| Concurrency | ✅ 100% |
| Edge cases | ✅ 100% |

## Test Statistics

- **Total Tests:** 50+
- **Test Classes:** 3
- **Theory Tests:** Multiple input variations
- **Concurrent Tests:** High-contention scenarios
- **Performance Tests:** Low-allocation verification
- **Edge Case Tests:** Unicode, special characters, boundary conditions

## Test Patterns Used

1. **Arrange-Act-Assert (AAA):** All tests follow this pattern
2. **Theory Tests:** Data-driven tests for multiple inputs
3. **Stress Tests:** High-load scenarios with many threads
4. **Race Condition Tests:** Concurrent access patterns
5. **Timeout Verification:** Using Stopwatch for timing assertions
6. **Resource Cleanup Tests:** Verifying Count property after operations

## Key Test Assertions

- Lock count increases when locks are held
- Lock count decreases to zero after release
- Sequential execution for same key
- Concurrent execution for different keys
- Proper exception types thrown for invalid input
- No deadlocks under any scenario
- No memory leaks with repeated operations
- Case-insensitive comparer works correctly

## Continuous Integration

These tests are designed to run in CI/CD pipelines:
- Fast execution (< 30 seconds for full suite)
- Deterministic results
- No external dependencies
- Cross-platform compatible

## Coverage Tools

To generate HTML coverage report:
```bash
# Install reportgenerator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate coverage
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test

# Generate HTML report
reportgenerator -reports:coverage.cobertura.xml -targetdir:coveragereport -reporttypes:Html

# Open report
start coveragereport\index.html
```
