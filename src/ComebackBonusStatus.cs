namespace LiveOpsTest;

public sealed class ComebackBonusStatus
{
    // true khi event dang active va nen hien cho player
    public bool IsUnlocked { get; init; }

    // True khi card cua ngay hien tai co the claim ngay bay gio.
    public bool CanClaim { get; init; }

    // true sau khi hoan thanh Day 3 va dang cho het cooldown.
    public bool IsInCooldown { get; init; }

    // True khi ngay tren may bi lui so voi lastKnownDate
    public bool IsTimeRollbackDetected { get; init; }

    // True khi player da hoan thanh toan bo chuoi reward
    public bool IsCompleted { get; init; }

    // ngay reward hien tai, dem tu 0: 0 = Day 1, 1 = Day 2, 2 = Day 3
    public int CurrentDayIndex { get; init; }

    // Ngay cooldown ket thuc sau khi hoan thanh event.
    public DateTime CooldownUntilDate { get; init; }

    // Ban copy trang thai da claim cho UI card, UI khong nen sua mang nay
    public bool[] ClaimedDays { get; init; } = [];
}
