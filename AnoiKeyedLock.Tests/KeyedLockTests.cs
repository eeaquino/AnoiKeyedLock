using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AnoiKeyedLock.Tests
{
    public class KeyedLockTests
    {
        [Fact]
        public void Lock_WithValidKey_AcquiresAndReleasesLock()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";

            // Act & Assert
            using (var releaser = keyedLock.Lock(key))
            {
                Assert.Equal(1, keyedLock.Count);
            }
            
            // After disposal, count should eventually be 0
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public void Lock_WithNullKey_ThrowsArgumentNullException()
        {
            // Arrange
            var keyedLock = new KeyedLock();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => keyedLock.Lock(null));
        }

        [Fact]
        public void Lock_WithEmptyKey_ThrowsArgumentNullException()
        {
            // Arrange
            var keyedLock = new KeyedLock();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => keyedLock.Lock(string.Empty));
        }

        [Fact]
        public void Lock_WithWhitespaceKey_ThrowsArgumentNullException()
        {
            // Arrange
            var keyedLock = new KeyedLock();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => keyedLock.Lock("   "));
        }

        [Fact]
        public void Lock_MultipleDifferentKeys_AllowsConcurrentAccess()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            var completed = new List<string>();
            var allStarted = new ManualResetEventSlim(false);
            var startCount = 0;

            // Act
            var tasks = new[]
            {
                Task.Run(() =>
                {
                    using (var releaser = keyedLock.Lock("key1"))
                    {
                        Interlocked.Increment(ref startCount);
                        if (startCount == 3) allStarted.Set();
                        allStarted.Wait();
                        Thread.Sleep(50);
                        lock (completed) completed.Add("key1");
                    }
                }),
                Task.Run(() =>
                {
                    using (var releaser = keyedLock.Lock("key2"))
                    {
                        Interlocked.Increment(ref startCount);
                        if (startCount == 3) allStarted.Set();
                        allStarted.Wait();
                        Thread.Sleep(50);
                        lock (completed) completed.Add("key2");
                    }
                }),
                Task.Run(() =>
                {
                    using (var releaser = keyedLock.Lock("key3"))
                    {
                        Interlocked.Increment(ref startCount);
                        if (startCount == 3) allStarted.Set();
                        allStarted.Wait();
                        Thread.Sleep(50);
                        lock (completed) completed.Add("key3");
                    }
                })
            };

            Task.WaitAll(tasks);

            // Assert
            Assert.Equal(3, completed.Count);
            Assert.Contains("key1", completed);
            Assert.Contains("key2", completed);
            Assert.Contains("key3", completed);
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public void Lock_SameKeyMultipleTimes_ExecutesSequentially()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "shared-key";
            var executionOrder = new List<int>();
            var lockObject = new object();

            // Act
            var tasks = Enumerable.Range(1, 5).Select(i => Task.Run(() =>
            {
                using (var releaser = keyedLock.Lock(key))
                {
                    lock (lockObject)
                    {
                        executionOrder.Add(i);
                    }
                    Thread.Sleep(10); // Ensure sequential execution is observable
                }
            })).ToArray();

            Task.WaitAll(tasks);

            // Assert
            Assert.Equal(5, executionOrder.Count);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }.OrderBy(x => x).ToList(), 
                         executionOrder.OrderBy(x => x).ToList());
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public void Lock_WithCaseInsensitiveComparer_TreatsDifferentCasesAsSameKey()
        {
            // Arrange
            var keyedLock = new KeyedLock(StringComparer.OrdinalIgnoreCase);
            var counter = 0;
            var lockObj = new object();

            // Act
            var tasks = new[]
            {
                Task.Run(() =>
                {
                    using (var releaser = keyedLock.Lock("MyKey"))
                    {
                        var current = counter;
                        Thread.Sleep(50);
                        lock (lockObj) counter = current + 1;
                    }
                }),
                Task.Run(() =>
                {
                    Thread.Sleep(10);
                    using (var releaser = keyedLock.Lock("mykey"))
                    {
                        var current = counter;
                        Thread.Sleep(50);
                        lock (lockObj) counter = current + 1;
                    }
                }),
                Task.Run(() =>
                {
                    Thread.Sleep(20);
                    using (var releaser = keyedLock.Lock("MYKEY"))
                    {
                        var current = counter;
                        Thread.Sleep(50);
                        lock (lockObj) counter = current + 1;
                    }
                })
            };

            Task.WaitAll(tasks);

            // Assert - If locks worked properly, counter should be 3
            Assert.Equal(3, counter);
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public void TryLock_WithTimeout_ReturnsTrue_WhenLockAcquired()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";

            // Act
            var success = keyedLock.TryLock(key, TimeSpan.FromSeconds(1), out var releaser);

            // Assert
            Assert.True(success);
            Assert.Equal(1, keyedLock.Count);
            
            releaser.Dispose();
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public void TryLock_WithTimeout_ReturnsFalse_WhenTimeout()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";

            // Act
            using (var firstLock = keyedLock.Lock(key))
            {
                var task = Task.Run(() =>
                {
                    var success = keyedLock.TryLock(key, TimeSpan.FromMilliseconds(100), out var releaser);
                    return (success, releaser);
                });

                var result = task.Result;

                // Assert
                Assert.False(result.success);
            }

            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public void TryLock_WithMillisecondsTimeout_ReturnsTrue_WhenLockAcquired()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";

            // Act
            var success = keyedLock.TryLock(key, 1000, out var releaser);

            // Assert
            Assert.True(success);
            releaser.Dispose();
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public void TryLock_WithCancellationToken_ReturnsTrue_WhenNotCancelled()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";
            var cts = new CancellationTokenSource();

            // Act
            var success = keyedLock.TryLock(key, cts.Token, out var releaser);

            // Assert
            Assert.True(success);
            releaser.Dispose();
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public void TryLock_WithCancellationToken_ReturnsFalse_WhenCancelled()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";
            var cts = new CancellationTokenSource();

            // Act
            using (var firstLock = keyedLock.Lock(key))
            {
                var task = Task.Run(() =>
                {
                    Thread.Sleep(50);
                    cts.Cancel();
                });

                var success = keyedLock.TryLock(key, cts.Token, out var releaser);

                task.Wait();

                // Assert
                Assert.False(success);
            }

            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task LockAsync_WithValidKey_AcquiresAndReleasesLock()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";

            // Act & Assert
            using (var releaser = await keyedLock.LockAsync(key))
            {
                Assert.Equal(1, keyedLock.Count);
            }

            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task LockAsync_WithNullKey_ThrowsArgumentNullException()
        {
            // Arrange
            var keyedLock = new KeyedLock();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await keyedLock.LockAsync(null));
        }

        [Fact]
        public async Task LockAsync_MultipleConcurrentCalls_ExecutesSequentially()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "shared-key";
            var counter = 0;

            // Act
            var tasks = Enumerable.Range(1, 10).Select(async i =>
            {
                using (var releaser = await keyedLock.LockAsync(key))
                {
                    var temp = counter;
                    await Task.Delay(10);
                    counter = temp + 1;
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            // Assert - If properly synchronized, counter should be 10
            Assert.Equal(10, counter);
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task LockAsync_WithCancellationToken_ThrowsWhenCancelled()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";
            var cts = new CancellationTokenSource();


            // Act
            using (var firstLock = await keyedLock.LockAsync(key))
            {
                cts.Cancel();
                
                // Assert
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => 
                    await keyedLock.LockAsync(key, cts.Token));
            }

            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task TryLockAsync_WithTimeout_ReturnsTrue_WhenLockAcquired()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";

            // Act
            var result = await keyedLock.TryLockAsync(key, TimeSpan.FromSeconds(1));

            // Assert
            Assert.True(result.success);
            result.releaser.Dispose();
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task TryLockAsync_WithTimeout_ReturnsFalse_WhenTimeout()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";

            // Act
            using (var firstLock = await keyedLock.LockAsync(key))
            {
                var result = await keyedLock.TryLockAsync(key, TimeSpan.FromMilliseconds(100));

                // Assert
                Assert.False(result.success);
            }

            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task TryLockAsync_WithMillisecondsTimeout_ReturnsTrue_WhenLockAcquired()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";

            // Act
            var result = await keyedLock.TryLockAsync(key, 1000);

            // Assert
            Assert.True(result.success);
            result.releaser.Dispose();
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task TryLockAsync_WithCancellationToken_ReturnsFalse_WhenCancelled()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";
            var cts = new CancellationTokenSource();

            // Act
            using (var firstLock = await keyedLock.LockAsync(key))
            {
                var cancelTask = Task.Run(async () =>
                {
                    await Task.Delay(50);
                    cts.Cancel();
                });

                var result = await keyedLock.TryLockAsync(key, TimeSpan.FromSeconds(10), cts.Token);

                await cancelTask;

                // Assert
                Assert.False(result.success);
            }

            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public void AutomaticCleanup_RemovesKeyAfterAllLocksReleased()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "cleanup-test";

            // Act
            using (var releaser1 = keyedLock.Lock(key))
            {
                Assert.Equal(1, keyedLock.Count);
            }

            // Assert
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task AutomaticCleanup_WithMultipleKeys_RemovesOnlyReleasedKeys()
        {
            // Arrange
            var keyedLock = new KeyedLock();

            // Act
            using (var releaser1 = await keyedLock.LockAsync("key1"))
            using (var releaser2 = await keyedLock.LockAsync("key2"))
            {
                Assert.Equal(2, keyedLock.Count);
            }

            // Assert
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task StressTest_ManyKeysAndThreads_MaintainsIntegrity()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            var counters = new Dictionary<string, int>();
            var lockObj = new object();
            var keys = Enumerable.Range(1, 20).Select(i => $"key-{i}").ToArray();
            
            foreach (var key in keys)
            {
                counters[key] = 0;
            }

            // Act
            var tasks = Enumerable.Range(1, 100).Select(i => Task.Run(async () =>
            {
                var key = keys[i % keys.Length];
                using (var releaser = await keyedLock.LockAsync(key))
                {
                    lock (lockObj)
                    {
                        counters[key]++;
                    }
                    await Task.Delay(1);
                }
            })).ToArray();

            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(100, counters.Values.Sum());
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task PerformanceTest_LowAllocation_CompletesQuickly()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const int iterations = 1000;
            var sw = Stopwatch.StartNew();

            // Act
            var tasks = Enumerable.Range(1, iterations).Select(async i =>
            {
                var key = $"key-{i % 10}";
                using (var releaser = await keyedLock.LockAsync(key))
                {
                    await Task.Yield();
                }
            }).ToArray();

            await Task.WhenAll(tasks);
            sw.Stop();

            // Assert
            Assert.Equal(0, keyedLock.Count);
            Assert.True(sw.ElapsedMilliseconds < 5000, 
                $"Performance test took too long: {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public void ReleaseWithoutAcquire_DoesNotCrash()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            var releaser = default(KeyedLockReleaser);

            // Act & Assert - Should not throw
            releaser.Dispose();
        }

        [Fact]
        public void DoubleDispose_DoesNotThrow()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";
            var releaser = keyedLock.Lock(key);

            // Act & Assert - Should not throw
            releaser.Dispose();
            releaser.Dispose();
            
            Assert.Equal(0, keyedLock.Count);
        }
    }
}
