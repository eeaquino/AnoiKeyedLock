using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AnoiKeyedLock.Tests
{
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddKeyedLock_RegistersIKeyedLockAsSingleton()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddKeyedLock();
            var provider = services.BuildServiceProvider();

            // Assert
            var keyedLock1 = provider.GetService<IKeyedLock>();
            var keyedLock2 = provider.GetService<IKeyedLock>();
            
            Assert.NotNull(keyedLock1);
            Assert.Same(keyedLock1, keyedLock2); // Singleton should return same instance
        }

        [Fact]
        public void AddKeyedLock_ReturnsServiceCollection_ForChaining()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var result = services.AddKeyedLock();

            // Assert
            Assert.Same(services, result);
        }

        [Fact]
        public void AddKeyedLock_WithComparer_UsesProvidedComparer()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddKeyedLock(StringComparer.OrdinalIgnoreCase);
            var provider = services.BuildServiceProvider();
            var keyedLock = provider.GetRequiredService<IKeyedLock>();

            // Assert - Case insensitive keys should be treated as same
            using (var releaser1 = keyedLock.Lock("TestKey"))
            {
                // "testkey" should block because it's the same key with case-insensitive comparer
                var tryResult = keyedLock.TryLock("testkey", TimeSpan.FromMilliseconds(50), out var releaser2);
                Assert.False(tryResult); // Should timeout because same key is locked
            }
        }

        [Fact]
        public void AddKeyedLock_WithNullComparer_UsesDefaultComparer()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddKeyedLock(null);
            var provider = services.BuildServiceProvider();
            var keyedLock = provider.GetRequiredService<IKeyedLock>();

            // Assert - Default comparer is case sensitive
            using (var releaser1 = keyedLock.Lock("TestKey"))
            {
                // "testkey" should succeed because it's a different key with case-sensitive comparer
                var tryResult = keyedLock.TryLock("testkey", TimeSpan.FromMilliseconds(100), out var releaser2);
                Assert.True(tryResult); // Should succeed because different key
                releaser2.Dispose();
            }
        }

        [Fact]
        public void AddKeyedLock_WithNullServices_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceCollection services = null!;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => services.AddKeyedLock());
        }

        [Fact]
        public void AddKeyedLock_WithComparerAndNullServices_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceCollection services = null!;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => services.AddKeyedLock(StringComparer.Ordinal));
        }

        [Fact]
        public void AddKeyedLock_WithConcurrencySettings_RegistersConfiguredInstance()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddKeyedLock(concurrencyLevel: 8, initialCapacity: 256);
            var provider = services.BuildServiceProvider();

            // Assert
            var keyedLock = provider.GetService<IKeyedLock>();
            Assert.NotNull(keyedLock);
        }

        [Fact]
        public void AddKeyedLock_WithConcurrencySettingsAndComparer_UsesProvidedComparer()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddKeyedLock(
                concurrencyLevel: 4, 
                initialCapacity: 100, 
                comparer: StringComparer.OrdinalIgnoreCase);
            var provider = services.BuildServiceProvider();
            var keyedLock = provider.GetRequiredService<IKeyedLock>();

            // Assert - Case insensitive
            using (var releaser1 = keyedLock.Lock("KEY"))
            {
                var tryResult = keyedLock.TryLock("key", TimeSpan.FromMilliseconds(50), out var releaser2);
                Assert.False(tryResult);
            }
        }

        [Fact]
        public void AddKeyedLock_WithConcurrencySettingsAndNullServices_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceCollection services = null!;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                services.AddKeyedLock(concurrencyLevel: 4, initialCapacity: 100));
        }

        [Fact]
        public void AddKeyedLock_CanBeResolvedAsKeyedLockType()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddKeyedLock();

            // Act
            var provider = services.BuildServiceProvider();
            var keyedLock = provider.GetService<IKeyedLock>();

            // Assert
            Assert.IsType<KeyedLock>(keyedLock);
        }

        [Fact]
        public void AddKeyedLock_WorksWithScopedServices()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddKeyedLock();

            var provider = services.BuildServiceProvider();

            // Act - Get from different scopes
            IKeyedLock keyedLock1;
            IKeyedLock keyedLock2;

            using (var scope1 = provider.CreateScope())
            {
                keyedLock1 = scope1.ServiceProvider.GetRequiredService<IKeyedLock>();
            }

            using (var scope2 = provider.CreateScope())
            {
                keyedLock2 = scope2.ServiceProvider.GetRequiredService<IKeyedLock>();
            }

            // Assert - Singleton should return same instance across scopes
            Assert.Same(keyedLock1, keyedLock2);
        }
    }
}
