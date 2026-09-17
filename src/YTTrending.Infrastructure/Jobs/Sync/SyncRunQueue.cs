using System.Threading.Channels;
using YTTrending.Application.Common.Interfaces.Jobs;
using Channel = System.Threading.Channels.Channel;

namespace YTTrending.Infrastructure.Jobs.Sync;

public sealed class SyncRunQueue : ISyncRunQueue
{
    // Queue in-memory chỉ đánh thức một worker; DB mới là source of truth.
    private readonly Channel<int> _queue = Channel.CreateUnbounded<int>(new()
    {
        SingleReader = true,
    });

    public void Enqueue(int syncRunId)
    {
        if (!_queue.Writer.TryWrite(syncRunId))
            throw new InvalidOperationException("Unable to enqueue sync run.");
    }

    public ValueTask<int> DequeueAsync(CancellationToken ct) => _queue.Reader.ReadAsync(ct);
}
