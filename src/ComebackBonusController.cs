using LiveOpsTest.Stubs;

namespace LiveOpsTest;

public sealed class ComebackBonusController : IDisposable
{
    // key de MockSaveController tim dung save object cua event nay.
    private const string SaveKey = "ComebackBonus";

    // Data cau hinh event, duoc gan khi goi Init
    private ComebackBonusConfig? config;

    // data tien do cua player, duoc load tu MockSaveController khi Init
    private ComebackBonusSave? save;

    // Lop boc thoi gian de test co the gia lap ngay va viec chinh gio
    private ITimeProvider timeProvider = new SystemTimeProvider();

    // Singleton don gian theo style cua skeleton.
    public static ComebackBonusController? Instance { get; private set; }

    // UI co the lang nghe event nay de ve lai card/nut sau khi status doi
    public static event Action<ComebackBonusStatus>? OnStatusChanged;

    // missions system co the lang nghe event nay khi player claim reward.
    public static event Action<int>? OnComebackBonusClaimed;

    // Trang thai runtime hien tai expose cho UI va test doc
    public ComebackBonusStatus Status { get; private set; } = new();

    public void Init(ComebackBonusConfig config, ITimeProvider? timeProvider = null)
    {
        if (Instance != null && Instance != this)
        {
            throw new InvalidOperationException("ComebackBonusController is already initialized.");
        }

        config.Validate();
        Instance = this;
        this.config = config;
        this.timeProvider = timeProvider ?? new SystemTimeProvider();
        save = MockSaveController.GetSaveObject<ComebackBonusSave>(SaveKey);
        save.Migrate(config.TotalDays);

        Refresh();
    }

    public void Refresh()
    {
        EnsureInitialized();
        Evaluate(timeProvider.Now, notifyChanged: true);
    }

    public bool TryClaim()
    {
        EnsureInitialized();//check null
        Evaluate(timeProvider.Now, notifyChanged: false);

        if (!Status.CanClaim)
        {
            return false;
        }

        var today = timeProvider.Now.Date;
        var claimedDayIndex = Status.CurrentDayIndex;
        var reward = config!.Rewards[claimedDayIndex];

        //key giải quyết edgecase cuối, nếu như claim thất bại trả false 
        if (!reward.TryClaim())
        {
            return false;
        }

        save!.claimedDays![claimedDayIndex] = true;
        save.lastClaimDate = today;

        if (claimedDayIndex >= config.TotalDays - 1)// key, ngày cuối, claim xong close event
        {                                           // rồi cooldown
            save.completed = true;
            save.eventActive = false;
            save.currentDayIndex = 0;
            save.cooldownUntilDate = today.AddDays(config.CooldownDays);//key
        }
        else
        {
            save.currentDayIndex = claimedDayIndex + 1;
        }

        save.lastKnownDate = MaxDate(save.lastKnownDate, today);
        save.lastLoginDate = today;
        MockSaveController.MarkAsSaveIsRequired();

        Status = BuildStatus(today, isRollback: false);
        OnComebackBonusClaimed?.Invoke(claimedDayIndex);//6. key
        NotifyChanged();
        return true;
    }

    public void Dispose()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void ResetForTests()
    {
        Instance = null;
        OnStatusChanged = null;
        OnComebackBonusClaimed = null;
    }

    private void Evaluate(DateTime now, bool notifyChanged)
    {
        // bỏ giờ phút s, tính theo lịch calendar 
        var today = now.Date;
        save!.Migrate(config!.TotalDays);

        if (!save.initialized)
        {
            save.initialized = true;
            save.lastLoginDate = today;
            save.lastKnownDate = today;
            save.currentDayIndex = 0;
            save.eventActive = false;// key, first time k trigger event
            save.completed = false;
            MockSaveController.MarkAsSaveIsRequired();
            Status = BuildStatus(today, isRollback: false);
            if (notifyChanged)
            {
                NotifyChanged();
            }

            return;//key
        }

        // chống lùi giờ, nếu detect thấy ngày hiện tại < ngày lớn nhất từng biết dc (key)
        var isRollback = save.lastKnownDate != default && today < save.lastKnownDate.Date;
        if (isRollback)
        {
            Status = BuildStatus(today, isRollback: true);
            if (notifyChanged)
            {
                NotifyChanged();// invoke callback
            }

            return;// không cho claim 
        }

        save.lastKnownDate = MaxDate(save.lastKnownDate, today);

        if (save.eventActive)
        {
            ReconcileActiveChain(today);//lastClaimdate
        }
        else if (CanTriggerNewEvent(today))
        {
            StartEvent(today);
        }

        save.lastLoginDate = today;
        MockSaveController.MarkAsSaveIsRequired();
        Status = BuildStatus(today, isRollback: false);

        if (notifyChanged)
        {
            NotifyChanged();
        }
    }

    private bool CanTriggerNewEvent(DateTime today)
    {
        if (save!.cooldownUntilDate != default && today < save.cooldownUntilDate.Date)
        {
            return false;
        }

        return CalculateMissedDays(save.lastLoginDate, today) >= config!.RequiredMissDays;
    }

    private void StartEvent(DateTime today)
    {
        save!.eventActive = true;
        save.completed = false;
        save.currentDayIndex = 0;
        save.lastClaimDate = default;
        save.claimedDays = new bool[config!.TotalDays];
        save.lastKnownDate = MaxDate(save.lastKnownDate, today);
    }

    private void ReconcileActiveChain(DateTime today)
    {
        if (save!.lastClaimDate == default)
        {
            save.currentDayIndex = 0;
            return;
        }

        var daysSinceClaim = CalculateMissedDays(save.lastClaimDate, today);
        if (daysSinceClaim > 1)
        {
            // chống cheat chỉnh giờ tới để claim ngày tiếp, neu cheat thì reset về ngày 1 
            StartEvent(today);
        }
    }

    private ComebackBonusStatus BuildStatus(DateTime today, bool isRollback)
    {
        var claimedDays = (bool[])save!.claimedDays!.Clone();
        var inCooldown = !save.eventActive &&
            save.cooldownUntilDate != default &&
            today < save.cooldownUntilDate.Date;

        return new ComebackBonusStatus
        {
            IsUnlocked = save.eventActive,
            IsInCooldown = inCooldown,
            IsCompleted = save.completed,
            IsTimeRollbackDetected = isRollback,
            CurrentDayIndex = save.currentDayIndex,
            CooldownUntilDate = save.cooldownUntilDate,
            ClaimedDays = claimedDays,
            CanClaim = save.eventActive && !isRollback && CanClaimToday(today)
        };
    }

    private bool CanClaimToday(DateTime today)
    {
        //Nếu event không active thì không claim được.
        //Kiểm tra ngày hiện tại đã claim chưa.
        if (!save!.eventActive || save.claimedDays![save.currentDayIndex])
        {
            return false;
        }
        //Đang ở Day 1 và chưa từng claim ngày nào
        if (save.currentDayIndex == 0 && save.lastClaimDate == default)
        {
            return true;
        }

        return save.lastClaimDate != default &&
            CalculateMissedDays(save.lastClaimDate, today) == 1;
        //kiểm tra ngày claim trước
        //phải từng claim trước đó rồi
        //Ngày hôm nay phải cách ngày claim trước đúng 1 ngày calendar
    }

  
    // Tính ngày bằng .Date, không tính đủ 24h
    private static int CalculateMissedDays(DateTime from, DateTime to)
    {
        return (to.Date - from.Date).Days;
    }

    private static DateTime MaxDate(DateTime left, DateTime right)
    {
        return left.Date >= right.Date ? left.Date : right.Date;
    }

    private void NotifyChanged()
    {
        OnStatusChanged?.Invoke(Status);
    }

    private void EnsureInitialized()
    {
        if (config == null || save == null)
        {
            throw new InvalidOperationException("ComebackBonusController.Init must be called first.");
        }
    }
}
