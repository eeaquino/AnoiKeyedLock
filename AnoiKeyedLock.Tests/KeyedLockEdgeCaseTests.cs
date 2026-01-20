using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AnoiKeyedLock.Tests
{
    public class KeyedLockEdgeCaseTests
    {
        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t")]
        [InlineData("\n")]
        [InlineData("   \t\n   ")]
        public void Lock_WithWhitespaceVariations_ThrowsArgumentNullException(string key)
        {
            // Arrange
            var keyedLock = new KeyedLock();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => keyedLock.Lock(key));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t")]
        public async Task LockAsync_WithWhitespaceVariations_ThrowsArgumentNullException(string key)
        {
            // Arrange
            var keyedLock = new KeyedLock();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => keyedLock.LockAsync(key));
        }

        [Fact]
        public void Lock_WithVeryLongKey_WorksCorrectly()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            var longKey = new string('a', 10000);

            // Act & Assert
            using (var releaser = keyedLock.Lock(longKey))
            {
                Assert.Equal(1, keyedLock.Count);
            }
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public void Lock_WithSpecialCharacters_WorksCorrectly()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            var specialKeys = new[]
            {
                "key-with-dashes",
                "key_with_underscores",
                "key.with.dots",
                "key/with/slashes",
                "key\\with\\backslashes",
                "key:with:colons",
                "key@with@at",
                "key#with#hash",
                "key$with$dollar",
                "key%with%percent",
                "key&with&ampersand",
                "key*with*asterisk",
                "key(with)parens",
                "key[with]brackets",
                "key{with}braces",
                "key<with>angles",
                "key|with|pipes",
                "key~with~tilde",
                "key`with`backtick",
                "key'with'quote",
                "key\"with\"doublequote",
                "key!with!exclamation",
                "key?with?question",
                "key=with=equals",
                "key+with+plus",
                "key,with,comma",
                "key;with;semicolon",
                "键盘锁", // Chinese characters
                "🔒🔑", // Emoji
                "Ключ", // Cyrillic
                "المفتاح" // Arabic
            };

            // Act & Assert
            foreach (var key in specialKeys)
            {
                using (var releaser = keyedLock.Lock(key))
                {
                    Assert.True(keyedLock.Count >= 1);
                }
            }
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public void CaseInsensitiveLock_WithVariousCasings_UseSameLock()
        {
            // Arrange
            var keyedLock = new KeyedLock(StringComparer.OrdinalIgnoreCase);
            var isBlocked = false;

            // Act
            using (var releaser1 = keyedLock.Lock("TestKey"))
            {
                var task = Task.Run(() =>
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    using (var releaser2 = keyedLock.Lock("testkey"))
                    {
                        isBlocked = sw.ElapsedMilliseconds > 10;
                    }
                });

                Thread.Sleep(100);
                // Task should still be blocked
                Assert.False(task.IsCompleted);
            }

            Thread.Sleep(50);
            
            // Assert
            Assert.True(isBlocked);
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task ZeroTimeout_ReturnsImmediately()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "zero-timeout-key";

            // Act
            using (var firstLock = await keyedLock.LockAsync(key))
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await keyedLock.TryLockAsync(key, TimeSpan.Zero);
                sw.Stop();

                // Assert
                Assert.False(result.success);
                Assert.True(sw.ElapsedMilliseconds < 100, 
                    $"Should return immediately but took {sw.ElapsedMilliseconds}ms");
            }

            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task NegativeTimeout_ThrowsOrReturnsFalseImmediately()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "negative-timeout-key";

            // Act & Assert
            try
            {
                // SemaphoreSlim may throw with negative timeout
                var result = await keyedLock.TryLockAsync(key, TimeSpan.FromMilliseconds(-1));
                // If it doesn't throw, it should return false
                Assert.False(result.success);
            }
            catch (ArgumentOutOfRangeException)
            {
                // This is also acceptable behavior
            }
        }

        [Fact]
        public void DefaultReleaser_CanBeDisposedMultipleTimes()
        {
            // Arrange
            var releaser = default(KeyedLockReleaser);

            // Act & Assert - Should not throw
            releaser.Dispose();
            releaser.Dispose();
            releaser.Dispose();
        }

        [Fact]
        public async Task ExceptionInCriticalSection_StillReleasesLock()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "exception-key";

            // Act
            try
            {
                using (var releaser = await keyedLock.LockAsync(key))
                {
                    throw new InvalidOperationException("Test exception");
                }
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

            // Assert - Lock should be released
            var acquired = false;
            var result = await keyedLock.TryLockAsync(key, TimeSpan.FromMilliseconds(100));
            if (result.success)
            {
                acquired = true;
                result.releaser.Dispose();
            }

            Assert.True(acquired, "Lock should be available after exception");
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task TaskCancellation_BeforeAcquiringLock_DoesNotLeakResources()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "cancel-before-acquire";
            var cts = new CancellationTokenSource();

            // Act
            using (var holdingLock = await keyedLock.LockAsync(key))
            {
                cts.Cancel(); // Cancel before trying to acquire
                
                try
                {
                    await keyedLock.LockAsync(key, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            // Assert
            await Task.Delay(50);
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public void SynchronousLock_OnThreadPool_DoesNotDeadlock()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "threadpool-key";
            var completed = false;

            // Act
            var task = Task.Run(() =>
            {
                using (var releaser = keyedLock.Lock(key))
                {
                    Thread.Sleep(100);
                    completed = true;
                }
            });

            task.Wait(TimeSpan.FromSeconds(2));

            // Assert
            Assert.True(completed);
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task MultipleInstancesOfKeyedLock_DoNotInteract()
        {
            // Arrange
            var keyedLock1 = new KeyedLock();
            var keyedLock2 = new KeyedLock();
            const string key = "shared-key-name";
            var bothHeld = false;

            // Act
            using (var releaser1 = await keyedLock1.LockAsync(key))
            {
                using (var releaser2 = await keyedLock2.LockAsync(key))
                {
                    // Both should be held simultaneously
                    bothHeld = true;
                }
            }

            // Assert
            Assert.True(bothHeld, "Different KeyedLock instances should not interact");
            Assert.Equal(0, keyedLock1.Count);
            Assert.Equal(0, keyedLock2.Count);
        }

        [Fact]
        public async Task RapidCreateAndDestroy_ManyKeys_NoLeaks()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const int keyCount = 1000;

            // Act
            for (int i = 0; i < keyCount; i++)
            {
                using (var releaser = await keyedLock.LockAsync($"key-{i}"))
                {
                    await Task.Yield();
                }
            }

            // Assert
            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public void TryLock_ImmediateTimeout_WithMilliseconds()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "immediate-timeout";

            // Act
            using (var firstLock = keyedLock.Lock(key))
            {
                var success = keyedLock.TryLock(key, 0, out var releaser);

                // Assert
                Assert.False(success);
            }

            Assert.Equal(0, keyedLock.Count);
        }

        [Fact]
        public async Task VeryLongTimeout_CanBeCancelled()
        {
            // Arrange
            var keyedLock = new KeyedLock();
            const string key = "long-timeout-key";
            var cts = new CancellationTokenSource();

            // Act
            using (var holdingLock = await keyedLock.LockAsync(key))
            {
                var waitTask = Task.Run(async () =>
                {
                    var result = await keyedLock.TryLockAsync(key, TimeSpan.FromHours(1), cts.Token);
                    return result.success;
                });

                await Task.Delay(100);
                cts.Cancel();
                
                var success = await waitTask;

                // Assert
                Assert.False(success);
            }

            Assert.Equal(0, keyedLock.Count);
        }
    }
}
