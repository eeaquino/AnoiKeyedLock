using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using AnoiKeyedLock;
using Microsoft.VSDiagnostics;

namespace AnoiKeyedLock.Benchmarks
{
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    [CPUUsageDiagnoser]
    public class KeyedLockBenchmarks
    {
        private KeyedLock _keyedLock;
        private const string SingleKey = "benchmark-key";
        private const int IterationsPerBatch = 100;
        [GlobalSetup]
        public void Setup()
        {
            _keyedLock = new KeyedLock();
        }

        [Benchmark(Description = "Single-threaded sequential lock/unlock")]
        public async Task SequentialLockUnlock()
        {
            for (int i = 0; i < IterationsPerBatch; i++)
            {
                using (var releaser = await _keyedLock.LockAsync(SingleKey))
                {
                    await Task.Yield();
                }
            }
        }

        [Benchmark(Description = "High contention - 10 parallel tasks, same key")]
        public async Task HighContention_SameKey()
        {
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        using (var releaser = await _keyedLock.LockAsync(SingleKey))
                        {
                            await Task.Yield();
                        }
                    }
                });
            }

            await Task.WhenAll(tasks);
        }

        [Benchmark(Description = "Low contention - 10 parallel tasks, different keys")]
        public async Task LowContention_DifferentKeys()
        {
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                int keyIndex = i;
                tasks[i] = Task.Run(async () =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        using (var releaser = await _keyedLock.LockAsync($"key-{keyIndex}"))
                        {
                            await Task.Yield();
                        }
                    }
                });
            }

            await Task.WhenAll(tasks);
        }

        [Benchmark(Description = "Mixed sync/async operations")]
        public async Task MixedSyncAsync()
        {
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                if (index % 2 == 0)
                {
                    tasks[i] = Task.Run(async () =>
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            using (var releaser = await _keyedLock.LockAsync(SingleKey))
                            {
                                await Task.Yield();
                            }
                        }
                    });
                }
                else
                {
                    tasks[i] = Task.Run(() =>
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            using (var releaser = _keyedLock.Lock(SingleKey))
                            {
                            // Synchronous work
                            }
                        }
                    });
                }
            }

            await Task.WhenAll(tasks);
        }

        [Benchmark(Description = "Rapid key churn - create and cleanup many keys")]
        public async Task RapidKeyChurn()
        {
            for (int i = 0; i < IterationsPerBatch; i++)
            {
                using (var releaser = await _keyedLock.LockAsync($"transient-key-{i}"))
                {
                    await Task.Yield();
                }
            }
        }

        [Benchmark(Description = "TryLock with timeout - successful acquisitions")]
        public async Task TryLock_Success()
        {
            for (int i = 0; i < IterationsPerBatch; i++)
            {
                var result = await _keyedLock.TryLockAsync(SingleKey, TimeSpan.FromSeconds(1));
                if (result.success)
                {
                    result.releaser.Dispose();
                }
            }
        }

        [Benchmark(Description = "TryLock with timeout - mix of success and failures")]
        public async Task TryLock_MixedResults()
        {
            var holdingTask = Task.Run(async () =>
            {
                using (var releaser = await _keyedLock.LockAsync(SingleKey))
                {
                    await Task.Delay(50);
                }
            });
            await Task.Delay(5);
            var tasks = new Task[20];
            for (int i = 0; i < 20; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    var result = await _keyedLock.TryLockAsync(SingleKey, TimeSpan.FromMilliseconds(10));
                    if (result.success)
                    {
                        result.releaser.Dispose();
                    }
                });
            }

            await Task.WhenAll(tasks);
            await holdingTask;
        }

        [Benchmark(Description = "Extreme load - 50 parallel tasks with high contention")]
        public async Task ExtremeLoad_HighContention()
        {
            var tasks = new Task[50];
            for (int i = 0; i < 50; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        using (var releaser = await _keyedLock.LockAsync(SingleKey))
                        {
                            await Task.Yield();
                        }
                    }
                });
            }

            await Task.WhenAll(tasks);
        }

        [Benchmark(Description = "Nested locks - multiple keys per operation")]
        public async Task NestedLocks_MultipleKeys()
        {
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    for (int j = 0; j < 5; j++)
                    {
                        using (var releaser1 = await _keyedLock.LockAsync("key-A"))
                        {
                            await Task.Yield();
                            using (var releaser2 = await _keyedLock.LockAsync("key-B"))
                            {
                                await Task.Yield();
                            }
                        }
                    }
                });
            }

            await Task.WhenAll(tasks);
        }

        [Benchmark(Description = "Memory stress - rapid allocation/deallocation")]
        public async Task MemoryStress_RapidAllocDealloc()
        {
            var tasks = new Task[20];
            for (int i = 0; i < 20; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    for (int j = 0; j < 50; j++)
                    {
                        using (var releaser = await _keyedLock.LockAsync($"stress-key-{j % 10}"))
                        {
                            await Task.Yield();
                        }
                    }
                });
            }

            await Task.WhenAll(tasks);
        }
    }
}