using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace FYP.Services
{
    public interface ICheckoutLockService
    {
        Task<bool> AcquireLockAsync(string userId, TimeSpan timeout);
        void ReleaseLock(string userId);
    }

    public class CheckoutLockService : ICheckoutLockService
    {
        private readonly IMemoryCache _cache;
        
        public CheckoutLockService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<bool> AcquireLockAsync(string userId, TimeSpan timeout)
        {
            var cacheKey = $"CheckoutLock_{userId}";
            
            var semaphore = _cache.GetOrCreate(cacheKey, entry =>
            {
                // Give it a sliding expiration so the semaphore object is garbage collected 
                // after the checkout finishes and sits idle.
                entry.SlidingExpiration = TimeSpan.FromMinutes(2);
                return new SemaphoreSlim(1, 1);
            });

            if (semaphore == null) return false;

            return await semaphore.WaitAsync(timeout);
        }

        public void ReleaseLock(string userId)
        {
            var cacheKey = $"CheckoutLock_{userId}";
            if (_cache.TryGetValue(cacheKey, out SemaphoreSlim semaphore))
            {
                // Only release if we actually hold it
                if (semaphore != null && semaphore.CurrentCount == 0)
                {
                    semaphore.Release();
                }
            }
        }
    }
}
