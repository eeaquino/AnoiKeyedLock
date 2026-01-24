using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AnoiKeyedLock.Tests
{
    public class KeyedLockApiTests
    {
        #region IsLocked Tests

        [Fact]
        public void IsLocked_WhenKeyNotLocked_ReturnsFalse()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";

            // Act
            var result = keyedLock.IsLocked(key);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsLocked_WhenKeyIsLocked_ReturnsTrue()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";

            // Act & Assert
            using (var releaser = keyedLock.Lock(key))
            {
                Assert.True(keyedLock.IsLocked(key));
            }
        }

        [Fact]
        public void IsLocked_AfterLockReleased_ReturnsFalse()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";

            // Act
            using (var releaser = keyedLock.Lock(key))
            {
                // Lock is held
            }

            // Assert
            Assert.False(keyedLock.IsLocked(key));
        }

        [Fact]
        public void IsLocked_WithNullKey_ReturnsFalse()
        {
            // Arrange
            var keyedLock = new KeyedLock();

            // Act
            var result = keyedLock.IsLocked(null!);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsLocked_WithEmptyKey_ReturnsFalse()
        {
            // Arrange
            var keyedLock = new KeyedLock();

            // Act
            var result = keyedLock.IsLocked(string.Empty);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsLocked_WithWhitespaceKey_ReturnsFalse()
        {
            // Arrange
            var keyedLock = new KeyedLock();

            // Act
            var result = keyedLock.IsLocked("   ");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsLocked_WithConcurrentAccess_ReturnsCorrectState()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "concurrent-key";
            var lockAcquired = new ManualResetEventSlim(false);
            var checkComplete = new ManualResetEventSlim(false);

            // Act
            var lockTask = Task.Run(async () =>
            {
                using (var releaser = await keyedLock.LockAsync(key))
                {
                    lockAcquired.Set();
                    checkComplete.Wait();
                }
            });

            lockAcquired.Wait();

            // Assert
            Assert.True(keyedLock.IsLocked(key));
            checkComplete.Set();

            await lockTask;
            Assert.False(keyedLock.IsLocked(key));
        }

        #endregion

        #region GetActiveKeys Tests

        [Fact]
        public void GetActiveKeys_WhenNoLocks_ReturnsEmptyArray()
        {
            // Arrange
            var keyedLock = new KeyedLock();

            // Act
            var result = keyedLock.GetActiveKeys();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetActiveKeys_WhenSingleLockHeld_ReturnsKey()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";

            // Act & Assert
            using (var releaser = keyedLock.Lock(key))
            {
                var keys = keyedLock.GetActiveKeys();
                Assert.Single(keys);
                Assert.Contains(key, keys);
            }
        }

        [Fact]
        public void GetActiveKeys_WhenMultipleLocksHeld_ReturnsAllKeys()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            var expectedKeys = new[] { "key1", "key2", "key3" };

            // Act & Assert
            using (var releaser1 = keyedLock.Lock("key1"))
            using (var releaser2 = keyedLock.Lock("key2"))
            using (var releaser3 = keyedLock.Lock("key3"))
            {
                var keys = keyedLock.GetActiveKeys();
                Assert.Equal(3, keys.Length);
                foreach (var key in expectedKeys)
                {
                    Assert.Contains(key, keys);
                }
            }
        }

        [Fact]
        public void GetActiveKeys_AfterLocksReleased_ReturnsEmptyArray()
        {
            // Arrange
            var keyedLock = new KeyedLock();

            // Act
            using (var releaser1 = keyedLock.Lock("key1"))
            using (var releaser2 = keyedLock.Lock("key2"))
            {
                // Locks held
            }

            // Assert
            var keys = keyedLock.GetActiveKeys();
            Assert.Empty(keys);
        }

        [Fact]
        public void GetActiveKeys_ReturnsSnapshot_NotLiveReference()
        {
            // Arrange
            var keyedLock = new KeyedLock();

            // Act
            string[] snapshotBeforeLock;
            string[] snapshotDuringLock;
            string[] snapshotAfterLock;

            snapshotBeforeLock = keyedLock.GetActiveKeys();

            using (var releaser = keyedLock.Lock("key1"))
            {
                snapshotDuringLock = keyedLock.GetActiveKeys();
            }

            snapshotAfterLock = keyedLock.GetActiveKeys();

            // Assert - Snapshots should be independent
            Assert.Empty(snapshotBeforeLock);
            Assert.Single(snapshotDuringLock);
            Assert.Empty(snapshotAfterLock);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithConcurrencyLevelAndCapacity_CreatesInstance()
        {
            // Arrange & Act
            var keyedLock = new KeyedLock(concurrencyLevel: 4, initialCapacity: 100);

            // Assert
            Assert.NotNull(keyedLock);
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public void Constructor_WithConcurrencyLevelAndCapacityAndComparer_CreatesInstance()
        {
            // Arrange & Act
            var keyedLock = new KeyedLock(
                concurrencyLevel: 4, 
                initialCapacity: 100, 
                comparer: StringComparer.OrdinalIgnoreCase);

            // Assert
            Assert.NotNull(keyedLock);
        }

        [Fact]
        public void Constructor_WithConcurrencyLevelAndCapacityAndComparer_UsesCaseInsensitiveKeys()
        {
            // Arrange
            var keyedLock = new KeyedLock(
                concurrencyLevel: 4, 
                initialCapacity: 100, 
                comparer: StringComparer.OrdinalIgnoreCase);
            var counter = 0;

            // Act - These should be treated as the same key
            var tasks = new[]
            {
                Task.Run(() =>
                {
                    using (var releaser = keyedLock.Lock("TestKey"))
                    {
                        var temp = counter;
                        Thread.Sleep(50);
                        Interlocked.Exchange(ref counter, temp + 1);
                    }
                }),
                Task.Run(() =>
                {
                    Thread.Sleep(10);
                    using (var releaser = keyedLock.Lock("testkey"))
                    {
                        var temp = counter;
                        Thread.Sleep(50);
                        Interlocked.Exchange(ref counter, temp + 1);
                    }
                })
            };

            Task.WaitAll(tasks);

            // Assert - Sequential execution means counter should be 2
            Assert.Equal(2, counter);
        }

        [Fact]
        public void Constructor_WithZeroConcurrencyLevel_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => 
                new KeyedLock(concurrencyLevel: 0, initialCapacity: 100));
        }

        [Fact]
        public void Constructor_WithNegativeConcurrencyLevel_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => 
                new KeyedLock(concurrencyLevel: -1, initialCapacity: 100));
        }

        [Fact]
        public void Constructor_WithNegativeInitialCapacity_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => 
                new KeyedLock(concurrencyLevel: 4, initialCapacity: -1));
        }

        #endregion

        #region TryLockAsync Tests

        [Fact]
        public async Task TryLockAsync_WithTimeSpan_ReturnsSuccess_WhenLockAvailable()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";

            // Act
            var (success, releaser) = await keyedLock.TryLockAsync(key, TimeSpan.FromSeconds(1));

            // Assert
            Assert.True(success);
            Assert.Equal(1, keyedLock.Count);
            
            releaser.Dispose();
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task TryLockAsync_WithTimeSpan_ReturnsFailure_WhenTimeout()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";
            var lockHeld = new ManualResetEventSlim(false);
            var canRelease = new ManualResetEventSlim(false);

            // Act
            var holdingTask = Task.Run(async () =>
            {
                using (var releaser = await keyedLock.LockAsync(key))
                {
                    lockHeld.Set();
                    canRelease.Wait();
                }
            });

            lockHeld.Wait();

            var (success, releaser) = await keyedLock.TryLockAsync(key, TimeSpan.FromMilliseconds(50));

            // Assert
            Assert.False(success);

            canRelease.Set();
            await holdingTask;
        }

        [Fact]
        public async Task TryLockAsync_WithMilliseconds_ReturnsSuccess_WhenLockAvailable()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";

            // Act
            var (success, releaser) = await keyedLock.TryLockAsync(key, 1000);

            // Assert
            Assert.True(success);
            releaser.Dispose();
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task TryLockAsync_WithCancellation_ReturnsFailure_WhenCancelled()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "test-key";
            var cts = new CancellationTokenSource();
            var lockHeld = new ManualResetEventSlim(false);

            // Act
            var holdingTask = Task.Run(async () =>
            {
                using (var releaser = await keyedLock.LockAsync(key))
                {
                    lockHeld.Set();
                    await Task.Delay(1000);
                }
            });

            lockHeld.Wait();
            
            // Cancel after a short delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(50);
                cts.Cancel();
            });

            var (success, releaser) = await keyedLock.TryLockAsync(key, TimeSpan.FromSeconds(10), cts.Token);

            // Assert
            Assert.False(success);
        }

        #endregion
    }
}
