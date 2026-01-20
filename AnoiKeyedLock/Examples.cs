using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AnoiKeyedLock.Examples
{
    /// <summary>
    /// Examples demonstrating various usage patterns of KeyedLock
    /// </summary>
    public static class KeyedLockExamples
    {
        /// <summary>
        /// Example 1: Basic synchronous locking
        /// </summary>
        public static void BasicSyncExample()
        {
            var keyedLock = new KeyedLock();

            // Simulate multiple threads trying to access the same resource
            var tasks = new Task[5];
            for (int i = 0; i < 5; i++)
            {
                int taskId = i;
                tasks[i] = Task.Run(() =>
                {
                    using (var releaser = keyedLock.Lock("shared-resource"))
                    {
                        Console.WriteLine($"Task {taskId} acquired lock");
                        Thread.Sleep(1000); // Simulate work
                        Console.WriteLine($"Task {taskId} releasing lock");
                    }
                });
            }

            Task.WaitAll(tasks);
        }

        /// <summary>
        /// Example 2: Async locking with different keys
        /// </summary>
        public static async Task AsyncMultiKeyExample()
        {
            var keyedLock = new KeyedLock();

            // These can run concurrently because they use different keys
            var task1 = ProcessResourceAsync(keyedLock, "resource-A", 1);
            var task2 = ProcessResourceAsync(keyedLock, "resource-B", 2);
            var task3 = ProcessResourceAsync(keyedLock, "resource-A", 3); // Will wait for task1

            await Task.WhenAll(task1, task2, task3);
        }

        private static async Task ProcessResourceAsync(KeyedLock keyedLock, string resourceKey, int taskId)
        {
            using (var releaser = await keyedLock.LockAsync(resourceKey))
            {
                Console.WriteLine($"Task {taskId} started processing {resourceKey}");
                await Task.Delay(2000); // Simulate async work
                Console.WriteLine($"Task {taskId} finished processing {resourceKey}");
            }
        }

        /// <summary>
        /// Example 3: Try lock with timeout
        /// </summary>
        public static void TryLockWithTimeoutExample()
        {
            var keyedLock = new KeyedLock();
            var key = "limited-resource";

            // First thread holds the lock
            var longTask = Task.Run(() =>
            {
                using (var releaser = keyedLock.Lock(key))
                {
                    Console.WriteLine("Long task acquired lock");
                    Thread.Sleep(5000); // Hold for 5 seconds
                    Console.WriteLine("Long task releasing lock");
                }
            });

            Thread.Sleep(500); // Ensure longTask gets the lock first

            // Second thread tries with a short timeout
            var shortTask = Task.Run(() =>
            {
                Console.WriteLine("Short task attempting to acquire lock...");
                if (keyedLock.TryLock(key, TimeSpan.FromSeconds(2), out var releaser))
                {
                    using (releaser)
                    {
                        Console.WriteLine("Short task acquired lock");
                    }
                }
                else
                {
                    Console.WriteLine("Short task timed out - lock not acquired");
                }
            });

            Task.WaitAll(longTask, shortTask);
        }

        /// <summary>
        /// Example 4: Cancellation support
        /// </summary>
        public static async Task CancellationExample()
        {
            var keyedLock = new KeyedLock();
            var key = "cancellable-resource";
            var cts = new CancellationTokenSource();

            // Start a task that holds the lock
            var holdingTask = Task.Run(async () =>
            {
                using (var releaser = await keyedLock.LockAsync(key))
                {
                    Console.WriteLine("Holding task acquired lock");
                    await Task.Delay(5000); // Hold for 5 seconds
                    Console.WriteLine("Holding task releasing lock");
                }
            });

            await Task.Delay(500); // Ensure holdingTask gets the lock first

            // Try to acquire with cancellation
            var waitingTask = Task.Run(async () =>
            {
                Console.WriteLine("Waiting task attempting to acquire lock...");
                try
                {
                    using (var releaser = await keyedLock.LockAsync(key, cts.Token))
                    {
                        Console.WriteLine("Waiting task acquired lock");
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Waiting task was cancelled");
                }
            });

            // Cancel after 2 seconds
            await Task.Delay(2000);
            cts.Cancel();
            Console.WriteLine("Cancellation requested");

            await Task.WhenAll(holdingTask, waitingTask);
        }

        /// <summary>
        /// Example 5: Real-world scenario - preventing duplicate file processing
        /// </summary>
        public static async Task FileProcessingExample()
        {
            var fileProcessingLock = new KeyedLock();
            
            // Simulate multiple requests to process the same files
            var files = new[] { "file1.txt", "file2.txt", "file1.txt", "file3.txt", "file2.txt" };
            var tasks = new Task[files.Length];

            for (int i = 0; i < files.Length; i++)
            {
                int index = i;
                string fileName = files[i];
                
                tasks[i] = Task.Run(async () =>
                {
                    Console.WriteLine($"Request {index} wants to process {fileName}");
                    
                    using (var releaser = await fileProcessingLock.LockAsync(fileName))
                    {
                        Console.WriteLine($"Request {index} is processing {fileName}");
                        await Task.Delay(2000); // Simulate file processing
                        Console.WriteLine($"Request {index} finished processing {fileName}");
                    }
                });
            }

            await Task.WhenAll(tasks);
            Console.WriteLine($"All files processed. Active locks: {fileProcessingLock.Count}");
        }

        /// <summary>
        /// Example 6: Using integer keys for user-specific operations
        /// </summary>
        public static async Task UserOperationsExample()
        {
            var userLock = new KeyedLock();

            // Simulate operations for different users
            var operations = new[]
            {
                (userId: "user-1", operation: "UpdateProfile"),
                (userId: "user-2", operation: "UpdateProfile"),
                (userId: "user-1", operation: "ChangePassword"), // Will wait for first user 1 operation
                (userId: "user-3", operation: "UpdateProfile"),
                (userId: "user-2", operation: "DeleteAccount")   // Will wait for first user 2 operation
            };

            var tasks = operations.Select(async (op, index) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Operation {index}: User {op.userId} wants to {op.operation}");
                
                using (var releaser = await userLock.LockAsync(op.userId))
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Operation {index}: User {op.userId} executing {op.operation}");
                    await Task.Delay(1500); // Simulate operation
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Operation {index}: User {op.userId} completed {op.operation}");
                }
            }).ToArray();

            await Task.WhenAll(tasks);
            Console.WriteLine($"All operations complete. Active locks: {userLock.Count}");
        }

        /// <summary>
        /// Example 7: Performance test showing low allocation and automatic cleanup
        /// </summary>
        public static async Task PerformanceExample()
        {
            var keyedLock = new KeyedLock();
            var iterations = 10000;
            
            Console.WriteLine($"Starting performance test with {iterations} iterations...");
            var startMemory = GC.GetTotalMemory(true);
            var startTime = DateTime.Now;

            var tasks = new Task[iterations];
            for (int i = 0; i < iterations; i++)
            {
                int index = i;
                tasks[i] = Task.Run(async () =>
                {
                    var key = $"key-{index % 100}"; // Use 100 different keys
                    using (var releaser = await keyedLock.LockAsync(key))
                    {
                        await Task.Delay(1); // Minimal work
                    }
                });
            }

            await Task.WhenAll(tasks);

            var endTime = DateTime.Now;
            var endMemory = GC.GetTotalMemory(true);
            var duration = (endTime - startTime).TotalSeconds;
            var memoryIncrease = (endMemory - startMemory) / 1024.0 / 1024.0;

            Console.WriteLine($"Completed {iterations} lock operations in {duration:F2} seconds");
            Console.WriteLine($"Operations per second: {iterations / duration:F0}");
            Console.WriteLine($"Memory increase: {memoryIncrease:F2} MB");
            Console.WriteLine($"Active locks remaining: {keyedLock.Count}");
        }
    }
}
