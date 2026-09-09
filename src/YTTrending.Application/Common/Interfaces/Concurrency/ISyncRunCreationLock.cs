namespace YTTrending.Application.Common.Interfaces.Concurrency;

public interface ISyncRunCreationLock
{
    // Khóa đoạn check-active + create để controller/scheduler không tạo hai run cùng lúc.
    bool TryAcquire();
    void Release();
}
