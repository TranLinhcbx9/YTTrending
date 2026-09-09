namespace YTTrending.Application.Common.Interfaces.Jobs;

public interface ISyncRunQueue
{
    // Chỉ truyền run id qua memory; item/progress thật luôn nằm trong DB.
    void Enqueue(int syncRunId);
    ValueTask<int> DequeueAsync(CancellationToken ct);
}
