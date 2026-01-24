using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AnoiKeyedLock
{
    /// <summary>
    /// Interface for keyed lock implementations that ensure exclusive access per key.
    /// </summary>
    public interface IKeyedLock
    {
        /// <summary>
        /// Acquires a lock for the specified key. Blocks until the lock is acquired.
        /// </summary>
        /// <param name="key">The key to lock on.</param>
        /// <returns>A disposable that releases the lock when disposed.</returns>
        KeyedLockReleaser Lock(string key);

        /// <summary>
        /// Tries to acquire a lock for the specified key with a timeout.
        /// </summary>
        /// <param name="key">The key to lock on.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <param name="releaser">The disposable that releases the lock when disposed.</param>
        /// <returns>True if the lock was acquired; otherwise, false.</returns>
        bool TryLock(string key, TimeSpan timeout, out KeyedLockReleaser releaser);

        /// <summary>
        /// Tries to acquire a lock for the specified key with a timeout in milliseconds.
        /// </summary>
        /// <param name="key">The key to lock on.</param>
        /// <param name="millisecondsTimeout">The maximum time in milliseconds to wait for the lock.</param>
        /// <param name="releaser">The disposable that releases the lock when disposed.</param>
        /// <returns>True if the lock was acquired; otherwise, false.</returns>
        bool TryLock(string key, int millisecondsTimeout, out KeyedLockReleaser releaser);

        /// <summary>
        /// Tries to acquire a lock for the specified key with a cancellation token.
        /// </summary>
        /// <param name="key">The key to lock on.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="releaser">The disposable that releases the lock when disposed.</param>
        /// <returns>True if the lock was acquired; false if cancelled.</returns>
        bool TryLock(string key, CancellationToken cancellationToken, out KeyedLockReleaser releaser);

        /// <summary>
        /// Asynchronously acquires a lock for the specified key.
        /// </summary>
        /// <param name="key">The key to lock on.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a disposable that releases the lock when disposed.</returns>
        ValueTask<KeyedLockReleaser> LockAsync(string key, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Tries to asynchronously acquire a lock for the specified key with a timeout.
        /// </summary>
        /// <param name="key">The key to lock on.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a tuple with a boolean indicating success and a disposable releaser.</returns>
        ValueTask<(bool success, KeyedLockReleaser releaser)> TryLockAsync(string key, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Tries to asynchronously acquire a lock for the specified key with a timeout in milliseconds.
        /// </summary>
        /// <param name="key">The key to lock on.</param>
        /// <param name="millisecondsTimeout">The maximum time in milliseconds to wait for the lock.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a tuple with a boolean indicating success and a disposable releaser.</returns>
        ValueTask<(bool success, KeyedLockReleaser releaser)> TryLockAsync(string key, int millisecondsTimeout, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets the current number of keys being tracked.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Checks if a lock is currently held for the specified key.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key is currently locked; otherwise, false.</returns>
        bool IsLocked(string key);

        /// <summary>
        /// Gets a snapshot of all keys that currently have active locks.
        /// </summary>
        /// <returns>An array of keys that are currently being tracked.</returns>
        string[] GetActiveKeys();
    }

    /// <summary>
    /// High-performance keyed lock implementation that ensures exclusive access per key.
    /// Automatically cleans up resources when locks are released.
    /// </summary>
    public sealed class KeyedLock : IKeyedLock
    {
        private readonly ConcurrentDictionary<string, RefCountedSemaphore> _semaphores;

        /// <summary>
        /// Initializes a new instance of the KeyedLock class with default (ordinal) string comparison.
        /// </summary>
        public KeyedLock() : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the KeyedLock class with a custom string comparer.
        /// </summary>
        /// <param name="comparer">The string comparer to use for keys (e.g., StringComparer.OrdinalIgnoreCase).</param>
        public KeyedLock(IEqualityComparer<string>? comparer)
        {
            _semaphores = new ConcurrentDictionary<string, RefCountedSemaphore>(comparer ?? StringComparer.Ordinal);
        }

        /// <summary>
        /// Initializes a new instance of the KeyedLock class with custom concurrency settings.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will update the dictionary concurrently.</param>
        /// <param name="initialCapacity">The initial number of elements the dictionary can contain.</param>
        /// <param name="comparer">The string comparer to use for keys (e.g., StringComparer.OrdinalIgnoreCase). If null, uses StringComparer.Ordinal.</param>
        public KeyedLock(int concurrencyLevel, int initialCapacity, IEqualityComparer<string>? comparer = null)
        {
            if (concurrencyLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(concurrencyLevel), "Concurrency level must be at least 1.");
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Initial capacity cannot be negative.");

            _semaphores = new ConcurrentDictionary<string, RefCountedSemaphore>(
                concurrencyLevel, initialCapacity, comparer ?? StringComparer.Ordinal);
        }

        /// <summary>
        /// Acquires a lock for the specified key. Blocks until the lock is acquired.
        /// </summary>
        /// <param name="key">The key to lock on.</param>
        /// <returns>A disposable that releases the lock when disposed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KeyedLockReleaser Lock(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));

            var semaphore = GetOrAdd(key);
            semaphore.Semaphore.Wait();
            return new KeyedLockReleaser(this, key, semaphore);
        }

        /// <summary>
        /// Tries to acquire a lock for the specified key with a timeout.
        /// </summary>
        /// <param name="key">The key to lock on.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <param name="releaser">The disposable that releases the lock when disposed.</param>
        /// <returns>True if the lock was acquired; otherwise, false.</returns>
        public bool TryLock(string key, TimeSpan timeout, out KeyedLockReleaser releaser)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));

            var semaphore = GetOrAdd(key);
            if (semaphore.Semaphore.Wait(timeout))
            {
                releaser = new KeyedLockReleaser(this, key, semaphore);
                return true;
            }

            ReleaseRefCount(key, semaphore);
            releaser = default(KeyedLockReleaser);
            return false;
        }

        /// <summary>
        /// Tries to acquire a lock for the specified key with a timeout in milliseconds.
        /// </summary>
        /// <param name="key">The key to lock on.</param>
        /// <param name="millisecondsTimeout">The maximum time in milliseconds to wait for the lock.</param>
        /// <param name="releaser">The disposable that releases the lock when disposed.</param>
        /// <returns>True if the lock was acquired; otherwise, false.</returns>
        public bool TryLock(string key, int millisecondsTimeout, out KeyedLockReleaser releaser)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));

            var semaphore = GetOrAdd(key);
            if (semaphore.Semaphore.Wait(millisecondsTimeout))
            {
                releaser = new KeyedLockReleaser(this, key, semaphore);
                return true;
            }

            ReleaseRefCount(key, semaphore);
            releaser = default(KeyedLockReleaser);
            return false;
        }

        /// <summary>
        /// Tries to acquire a lock for the specified key with a cancellation token.
        /// </summary>
        /// <param name="key">The key to lock on.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="releaser">The disposable that releases the lock when disposed.</param>
        /// <returns>True if the lock was acquired; false if cancelled.</returns>
        public bool TryLock(string key, CancellationToken cancellationToken, out KeyedLockReleaser releaser)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));

            var semaphore = GetOrAdd(key);
            try
            {
                semaphore.Semaphore.Wait(cancellationToken);
                releaser = new KeyedLockReleaser(this, key, semaphore);
                return true;
            }
            catch (OperationCanceledException)
            {
                ReleaseRefCount(key, semaphore);
                releaser = default(KeyedLockReleaser);
                return false;
            }
        }

        /// <summary>
        /// Asynchronously acquires a lock for the specified key.
        /// </summary>
        /// <param name="key">The key to lock on.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a disposable that releases the lock when disposed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<KeyedLockReleaser> LockAsync(string key, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));

            var semaphore = GetOrAdd(key);
            try
            {
                await semaphore.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                return new KeyedLockReleaser(this, key, semaphore);
            }
            catch
            {
                ReleaseRefCount(key, semaphore);
                throw;
            }
        }

        /// <summary>
        /// Tries to asynchronously acquire a lock for the specified key with a timeout.
        /// </summary>
        /// <param name="key">The key to lock on.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a tuple with a boolean indicating success and a disposable releaser.</returns>
        public async ValueTask<(bool success, KeyedLockReleaser releaser)> TryLockAsync(string key, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));

            var semaphore = GetOrAdd(key);
            try
            {
                if (await semaphore.Semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false))
                {
                    return (true, new KeyedLockReleaser(this, key, semaphore));
                }
            }
            catch (OperationCanceledException)
            {
                ReleaseRefCount(key, semaphore);
                return (false, default(KeyedLockReleaser));
            }

            ReleaseRefCount(key, semaphore);
            return (false, default(KeyedLockReleaser));
        }

        /// <summary>
        /// Tries to asynchronously acquire a lock for the specified key with a timeout in milliseconds.
        /// </summary>
        /// <param name="key">The key to lock on.</param>
        /// <param name="millisecondsTimeout">The maximum time in milliseconds to wait for the lock.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a tuple with a boolean indicating success and a disposable releaser.</returns>
        public async ValueTask<(bool success, KeyedLockReleaser releaser)> TryLockAsync(string key, int millisecondsTimeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));

            var semaphore = GetOrAdd(key);
            try
            {
                if (await semaphore.Semaphore.WaitAsync(millisecondsTimeout, cancellationToken).ConfigureAwait(false))
                {
                    return (true, new KeyedLockReleaser(this, key, semaphore));
                }
            }
            catch (OperationCanceledException)
            {
                ReleaseRefCount(key, semaphore);
                return (false, default(KeyedLockReleaser));
            }

            ReleaseRefCount(key, semaphore);
            return (false, default(KeyedLockReleaser));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private RefCountedSemaphore GetOrAdd(string key)
        {
            while (true)
            {
                if (_semaphores.TryGetValue(key, out var semaphore))
                {
                    if (semaphore.TryAddRef())
                    {
                        return semaphore;
                    }
                    // Semaphore is being removed, retry
                    continue;
                }

                var newSemaphore = new RefCountedSemaphore();
                if (_semaphores.TryAdd(key, newSemaphore))
                {
                    return newSemaphore;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Release(string key, RefCountedSemaphore semaphore)
        {
            try
            {
                semaphore.Semaphore?.Release();
            }
            catch (ObjectDisposedException)
            {
                // Semaphore was already disposed, ignore
            }
            catch (SemaphoreFullException)
            {
                // Semaphore was released more times than acquired, ignore
            }

            if (semaphore.Release())
            {
                // Last reference, try to remove from dictionary
                ((ICollection<KeyValuePair<string, RefCountedSemaphore>>)_semaphores)
                    .Remove(new KeyValuePair<string, RefCountedSemaphore>(key, semaphore));

                semaphore.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReleaseRefCount(string key, RefCountedSemaphore semaphore)
        {
            if (semaphore.Release())
            {
                // Last reference, try to remove from dictionary
                ((ICollection<KeyValuePair<string, RefCountedSemaphore>>)_semaphores)
                    .Remove(new KeyValuePair<string, RefCountedSemaphore>(key, semaphore));

                semaphore.Dispose();
            }
        }

        /// <summary>
        /// Gets the current number of keys being tracked.
        /// </summary>
        public int Count => _semaphores.Count;

        /// <summary>
        /// Checks if a lock is currently held for the specified key.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key is currently locked; otherwise, false.</returns>
        /// <remarks>
        /// This is a point-in-time check and the lock state may change immediately after this method returns.
        /// Do not use this method for synchronization decisions.
        /// </remarks>
        public bool IsLocked(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            return _semaphores.TryGetValue(key, out var semaphore) && semaphore.Semaphore?.CurrentCount == 0;
        }

        /// <summary>
        /// Gets a snapshot of all keys that currently have active locks.
        /// </summary>
        /// <returns>An array of keys that are currently being tracked.</returns>
        /// <remarks>
        /// This returns a snapshot of keys at the moment of the call. Keys may be added or removed
        /// immediately after this method returns. Useful for diagnostics and monitoring.
        /// </remarks>
        public string[] GetActiveKeys()
        {
            return _semaphores.Keys.ToArray();
        }

        internal sealed class RefCountedSemaphore : IDisposable
        {
            private int _refCount;
            private SemaphoreSlim? _semaphore;

            public RefCountedSemaphore()
            {
                _semaphore = new SemaphoreSlim(1, 1);
                _refCount = 1;
            }

            public SemaphoreSlim? Semaphore => _semaphore;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryAddRef()
            {
                while (true)
                {
                    var current = Volatile.Read(ref _refCount);
                    if (current == 0)
                    {
                        return false; // Being disposed
                    }

                    if (Interlocked.CompareExchange(ref _refCount, current + 1, current) == current)
                    {
                        return true;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Release()
            {
                var newCount = Interlocked.Decrement(ref _refCount);
                return newCount == 0;
            }

            public void Dispose()
            {
                var sem = Interlocked.Exchange(ref _semaphore, null);
                sem?.Dispose();
            }
        }
    }

    /// <summary>
    /// Disposable struct that releases a keyed lock when disposed.
    /// Using a struct avoids heap allocation.
    /// </summary>
#if NET8_0_OR_GREATER
    public struct KeyedLockReleaser : IDisposable, IAsyncDisposable
#else
    public struct KeyedLockReleaser : IDisposable
#endif
    {
        private readonly KeyedLock _keyedLock;
        private readonly string _key;
        private readonly KeyedLock.RefCountedSemaphore _semaphore;
        private bool _disposed;

        internal KeyedLockReleaser(KeyedLock keyedLock, string key, KeyedLock.RefCountedSemaphore semaphore)
        {
            _keyedLock = keyedLock;
            _key = key;
            _semaphore = semaphore;
            _disposed = false;
        }

        /// <summary>
        /// Releases the lock.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (!_disposed && _keyedLock != null && _semaphore != null)
            {
                _disposed = true;
                _keyedLock.Release(_key, _semaphore);
            }
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// Asynchronously releases the lock.
        /// </summary>
        /// <returns>A ValueTask representing the asynchronous dispose operation.</returns>
        /// <remarks>
        /// This implementation is synchronous as the underlying release operation is fast.
        /// It is provided to enable the use of 'await using' syntax.
        /// </remarks>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
#endif
    }
}
