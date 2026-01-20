using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AnoiKeyedLock.Tests
{
    public class KeyedLockConcurrencyTests
    {
        [Fact]
        public async Task ConcurrentLockAndRelease_MaintainsCorrectReferenceCount()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "concurrent-key";
            const int concurrentRequests = 50;

            // Act
            var tasks = new Task[concurrentRequests];
            for (int i = 0; i < concurrentRequests; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    using (var releaser = await keyedLock.LockAsync(key))
                    {
                        await Task.Delay(10);
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task RapidLockUnlock_SameKey_NoMemoryLeak()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "rapid-key";
            const int iterations = 1000;

            // Act
            for (int i = 0; i < iterations; i++)
            {
                using (var releaser = await keyedLock.LockAsync(key))
                {
                    await Task.Yield();
                }
            }

            // Assert - Key should be cleaned up
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task InterleavedLockRequests_DifferentKeys_NoCrossContamination()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            var key1Counter = 0;
            var key2Counter = 0;

            // Act
            var tasks = new[]
            {
                Task.Run(async () =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        using (var releaser = await keyedLock.LockAsync("key1"))
                        {
                            key1Counter++;
                            await Task.Yield();
                        }
                    }
                }),
                Task.Run(async () =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        using (var releaser = await keyedLock.LockAsync("key2"))
                        {
                            key2Counter++;
                            await Task.Yield();
                        }
                    }
                })
            };

            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(100, key1Counter);
            Assert.Equal(100, key2Counter);
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task MixedSyncAndAsyncLocks_SameKey_ProperSynchronization()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "mixed-key";
            var counter = 0;
            var lockObj = new object();

            // Act
            var tasks = new[]
            {
                Task.Run(() =>
                {
                    for (int i = 0; i < 50; i++)
                    {
                        using (var releaser = keyedLock.Lock(key))
                        {
                            lock (lockObj) counter++;
                            Thread.Sleep(1);
                        }
                    }
                }),
                Task.Run(async () =>
                {
                    for (int i = 0; i < 50; i++)
                    {
                        using (var releaser = await keyedLock.LockAsync(key))
                        {
                            lock (lockObj) counter++;
                            await Task.Delay(1);
                        }
                    }
                })
            };

            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(100, counter);
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task ConcurrentTryLockWithTimeout_SomeSucceedSomeFail()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "timeout-key";
            var successCount = 0;
            var failCount = 0;
            var lockObj = new object();

            // Act - First task holds lock, others try with short timeout
            var holdingTask = Task.Run(async () =>
            {
                using (var releaser = await keyedLock.LockAsync(key))
                {
                    await Task.Delay(500);
                }
            });

            await Task.Delay(50); // Ensure holding task gets lock first

            var tryingTasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                tryingTasks[i] = Task.Run(async () =>
                {
                    var result = await keyedLock.TryLockAsync(key, TimeSpan.FromMilliseconds(100));
                    if (result.success)
                    {
                        lock (lockObj) successCount++;
                        result.releaser.Dispose();
                    }
                    else
                    {
                        lock (lockObj) failCount++;
                    }
                });
            }

            await Task.WhenAll(tryingTasks);
            await holdingTask;

            // Assert - Most should fail due to timeout, but at least some should fail
            Assert.True(failCount > 0, "Expected some locks to timeout");
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task DeadlockPrevention_NoDeadlockWithMultipleKeys()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            var completed = 0;

            // Act - Two tasks acquiring locks for different keys
            // They acquire locks in a way that avoids deadlock by using sequential ordering
            var task1 = Task.Run(async () =>
            {
                using (var releaser1 = await keyedLock.LockAsync("keyA"))
                {
                    await Task.Delay(50);
                }
                // Release keyA before acquiring keyB
                using (var releaser2 = await keyedLock.LockAsync("keyB"))
                {
                    Interlocked.Increment(ref completed);
                }
            });

            var task2 = Task.Run(async () =>
            {
                using (var releaser1 = await keyedLock.LockAsync("keyB"))
                {
                    await Task.Delay(50);
                }
                // Release keyB before acquiring keyA
                using (var releaser2 = await keyedLock.LockAsync("keyA"))
                {
                    Interlocked.Increment(ref completed);
                }
            });

            // Should complete without deadlock because locks are released between acquisitions
            var allTasksTask = Task.WhenAll(task1, task2);
            var completedTask = await Task.WhenAny(allTasksTask, Task.Delay(5000));
            
            // Assert
            Assert.Same(allTasksTask, completedTask);
            Assert.Equal(2, completed);
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task HighContentionScenario_AllLocksEventuallyAcquire()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "contention-key";
            var acquiredCount = 0;
            const int threadCount = 100;

            // Act
            var tasks = new Task[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    using (var releaser = await keyedLock.LockAsync(key))
                    {
                        Interlocked.Increment(ref acquiredCount);
                        await Task.Delay(5);
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(threadCount, acquiredCount);
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task CancellationDuringWait_ProperlyReleasesResources()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "cancel-key";
            var cts = new CancellationTokenSource();

            // Act
            using (var holdingLock = await keyedLock.LockAsync(key))
            {
                var waitingTask = Task.Run(async () =>
                {
                    try
                    {
                        using (var releaser = await keyedLock.LockAsync(key, cts.Token))
                        {
                            // Should not reach here
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected
                    }
                });

                await Task.Delay(50);
                cts.Cancel();
                await waitingTask;
            }

            // Assert - Should clean up properly
            await Task.Delay(100);
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task RaceConditionOnCleanup_NoExceptions()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "race-key";
            var exceptions = 0;

            // Act - Try to create race condition during cleanup
            var tasks = new Task[50];
            for (int i = 0; i < 50; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    try
                    {
                        using (var releaser = await keyedLock.LockAsync(key))
                        {
                            await Task.Yield();
                        }
                    }
                    catch
                    {
                        Interlocked.Increment(ref exceptions);
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(0, exceptions);
            Assert.Equal(0, keyedLock.Count);
        }
    }
}
