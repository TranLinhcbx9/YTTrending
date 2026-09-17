using YTTrending.Application.Common.Interfaces.Concurrency;

namespace YTTrending.Infrastructure.Concurrency;

public sealed class SyncRunCreationLock : ISyncRunCreationLock
{
    // 0 = rảnh, 1 = đang tạo run; Interlocked giúp singleton an toàn giữa request/job.
    private int _isHeld;

    public bool TryAcquire() => Interlocked.CompareExchange(ref _isHeld, 1, 0) == 0;
    public void Release() => Volatile.Write(ref _isHeld, 0);
}
