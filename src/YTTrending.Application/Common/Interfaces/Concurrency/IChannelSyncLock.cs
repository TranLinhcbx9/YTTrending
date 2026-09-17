namespace YTTrending.Application.Common.Interfaces.Concurrency;

public interface IChannelSyncLock
{
    /// <summary>True = chiếm được khoá, caller phải gọi Release trong finally. False = đang có lượt khác giữ.</summary>
    bool TryAcquire(int channelId);
    void Release(int channelId);
}
