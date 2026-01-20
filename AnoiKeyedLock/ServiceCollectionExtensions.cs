using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace AnoiKeyedLock
{
    /// <summary>
    /// Extension methods for configuring KeyedLock services in an <see cref="IServiceCollection" />.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds KeyedLock as a singleton service with default (ordinal) string comparison.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddKeyedLock(this IServiceCollection services)
        {
            return AddKeyedLock(services, null);
        }

        /// <summary>
        /// Adds KeyedLock as a singleton service with a custom string comparer.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <param name="comparer">The string comparer to use for keys (e.g., StringComparer.OrdinalIgnoreCase). If null, uses StringComparer.Ordinal.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddKeyedLock(this IServiceCollection services, IEqualityComparer<string> comparer)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IKeyedLock>(provider => new KeyedLock(comparer));
            return services;
        }
    }
}
