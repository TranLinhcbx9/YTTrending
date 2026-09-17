using System.Collections.Concurrent;
using YTTrending.Application.Common.Interfaces.Concurrency;

namespace YTTrending.Infrastructure.Concurrency;

public sealed class ChannelSyncLock : IChannelSyncLock
{
    private readonly ConcurrentDictionary<int, byte> _locked = new();

    public bool TryAcquire(int channelId) => _locked.TryAdd(channelId, 0);

    public void Release(int channelId) => _locked.TryRemove(channelId, out _);
}
