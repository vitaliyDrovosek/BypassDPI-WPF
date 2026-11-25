namespace NetFix.Services;

/// <summary>
/// Discord Rich Presence отключён в этом форке.
/// Сервис не подключается к Discord и не отправляет никаких статусов,
/// рекламных текстов или ссылок. Методы оставлены как заглушки, чтобы
/// существующие вызовы в приложении ничего не ломали.
/// </summary>
public class DiscordRpcService : IDisposable
{
    public bool IsPriorityMode { get; set; }
    public bool IsScanning { get; set; }

    public void Initialize() { }

    public void Enable() { }

    public void Disable() { }

    public void UpdatePing(int ping) { }

    public void SetMainMenu() { }

    public void SetDiagnostics(int percent, int problems) { }

    public void SetFixing() { }

    public void SetAllGood(bool zapret, bool tgws) { }

    public void SetProblems(string what) { }




    public void Dispose() { }
}
