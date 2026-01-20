# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2025-01-XX

### Added
- Initial release of AnoiKeyedLock
- Thread-safe keyed locking mechanism based on string keys
- Synchronous `Lock()` method for blocking lock acquisition
- Asynchronous `LockAsync()` method for non-blocking lock acquisition
- `TryLock()` methods with timeout support (TimeSpan and milliseconds)
- `TryLockAsync()` methods with timeout and cancellation token support
- Automatic cleanup of unused lock entries
- Support for custom `IEqualityComparer<string>` for key comparison
- `IKeyedLock` interface for dependency injection scenarios
- Extension methods for `IServiceCollection` to register keyed lock
- `Count` property to track active locks
- Comprehensive test suite with 100+ test cases
- Support for .NET Standard 2.1 and higher

### Performance
- Optimized for high-concurrency scenarios
- Minimal allocations using object pooling patterns
- Efficient cleanup mechanism using reference counting

### Documentation
- Comprehensive README with usage examples
- XML documentation comments for all public APIs
- Benchmark suite for performance testing

[Unreleased]: https://github.com/eeaquino/AnoiKeyedLock/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/eeaquino/AnoiKeyedLock/releases/tag/v1.0.0
