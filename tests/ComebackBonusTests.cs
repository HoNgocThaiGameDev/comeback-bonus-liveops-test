using LiveOpsTest.Stubs;

namespace LiveOpsTest.Tests;

public sealed class ComebackBonusTests
{
    private readonly ComebackBonusConfig config = new();
    private MockTimeProvider time = null!;
    private ComebackBonusController controller = null!;

    [SetUp]
    public void SetUp()
    {
        MockSaveController.Reset();
        ComebackBonusController.ResetForTests();
        time = new MockTimeProvider(new DateTime(2026, 5, 1, 10, 0, 0));
        controller = new ComebackBonusController();
    }

    [TearDown]
    public void TearDown()
    {
        controller.Dispose();
        ComebackBonusController.ResetForTests();
        MockSaveController.Reset();
    }

    // Kiểm tra player lần đầu mở game sau khi có feature thì chỉ init save, không unlock event.
    [Test]
    public void FirstTimeUser_DoesNotTriggerComebackBonus()
    {
        controller.Init(config, time);

        Assert.Multiple(() =>
        {
            Assert.That(controller.Status.IsUnlocked, Is.False);
            Assert.That(controller.Status.CanClaim, Is.False);
            Assert.That(controller.Status.CurrentDayIndex, Is.EqualTo(0));
        });
    }

    // Kiểm tra player vắng đúng 3 ngày lịch thì Comeback Bonus unlock ở Day 1.
    [Test]
    public void UserMissesThreeDays_UnlocksDayOne()
    {
        controller.Init(config, time);

        time.Now = new DateTime(2026, 5, 4, 9, 0, 0);
        controller.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(controller.Status.IsUnlocked, Is.True);
            Assert.That(controller.Status.CurrentDayIndex, Is.EqualTo(0));
            Assert.That(controller.Status.CanClaim, Is.True);
        });
    }

    // Kiểm tra player chỉ vắng 2 ngày thì chưa đủ điều kiện unlock event.
    [Test]
    public void UserMissesTwoDays_DoesNotUnlock()
    {
        controller.Init(config, time);

        time.Now = new DateTime(2026, 5, 3, 9, 0, 0);
        controller.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(controller.Status.IsUnlocked, Is.False);
            Assert.That(controller.Status.CanClaim, Is.False);
        });
    }

    // Kiểm tra claim Day 1 lưu progress đúng và qua ngày mới có thể claim Day 2.
    [Test]
    public void ClaimDayOne_SavesProgress_ThenNextDayCanClaimDayTwo()
    {
        UnlockEventOnMayFour();
        //eventActive = true
        //currentDayIndex = 0
        //claimedDays = [false, false, false]
        //CanClaim = true
        Assert.That(controller.TryClaim(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(controller.Status.ClaimedDays[0], Is.True);
            Assert.That(controller.Status.CurrentDayIndex, Is.EqualTo(1));
            Assert.That(controller.Status.CanClaim, Is.False);
        });

        time.Now = new DateTime(2026, 5, 5, 8, 0, 0);
        controller.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(controller.Status.CurrentDayIndex, Is.EqualTo(1));
            Assert.That(controller.Status.CanClaim, Is.True);
            Assert.That(controller.TryClaim(), Is.True);
            Assert.That(controller.Status.ClaimedDays[1], Is.True);
        });
    }

    // Kiểm tra reset ngày theo mốc 00:00: claim 23:59 thì 00:01 ngày sau claim được Day 2.
    [Test]
    public void ClaimDayOneAt2359_AfterMidnightCanClaimDayTwo()
    {
        controller.Init(config, time);
        time.Now = new DateTime(2026, 5, 4, 23, 59, 0);
        controller.Refresh();

        Assert.That(controller.TryClaim(), Is.True);

        time.Now = new DateTime(2026, 5, 5, 0, 1, 0);
        controller.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(controller.Status.CurrentDayIndex, Is.EqualTo(1));
            Assert.That(controller.Status.CanClaim, Is.True);
        });
    }

    // Kiểm tra chỉnh giờ tới/skip ngày trong chuỗi không được claim nhanh, chain reset về Day 1.
    [Test]
    public void ForwardClockSkipDay_DoesNotClaimNextDayAndResetsChain()
    {
        UnlockEventOnMayFour();
        //eventActive = true
        //currentDayIndex = 0
        //claimedDays = [false, false, false]
        //CanClaim = true
        Assert.That(controller.TryClaim(), Is.True);

        time.Now = new DateTime(2026, 5, 6, 8, 0, 0);
        controller.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(controller.Status.IsUnlocked, Is.True);
            Assert.That(controller.Status.CurrentDayIndex, Is.EqualTo(0));
            Assert.That(controller.Status.ClaimedDays, Is.All.False);
            Assert.That(controller.Status.CanClaim, Is.True);
        });
    }

    // Kiểm tra chỉnh giờ lùi vẫn giữ progress đã claim nhưng không cho claim lại.
    [Test]
    public void RollbackClock_KeepsProgressAndCannotClaimAgain()
    {
        UnlockEventOnMayFour();
        //eventActive = true
        //currentDayIndex = 0
        //claimedDays = [false, false, false]
        //CanClaim = true
        Assert.That(controller.TryClaim(), Is.True);
        //lastClaimDate = 2026-05-04
        //lastKnownDate = 2026 - 05 - 04
        //currentDayIndex = 1
        //claimedDays[0] = true
        time.Now = new DateTime(2026, 5, 3, 8, 0, 0);
        controller.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(controller.Status.IsTimeRollbackDetected, Is.True);
            Assert.That(controller.Status.ClaimedDays[0], Is.True);
            Assert.That(controller.Status.CurrentDayIndex, Is.EqualTo(1));
            Assert.That(controller.Status.CanClaim, Is.False);
            Assert.That(controller.TryClaim(), Is.False);
        });

        time.Now = new DateTime(2026, 5, 5, 8, 0, 0);
        controller.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(controller.Status.IsTimeRollbackDetected, Is.False);
            Assert.That(controller.Status.CurrentDayIndex, Is.EqualTo(1));
            Assert.That(controller.Status.CanClaim, Is.True);
        });
    }

    // Kiểm tra bỏ lỡ ngày giữa chuỗi sau khi claim Day 1 thì event reset về Day 1.
    [Test]
    public void MissMiddleDayAfterClaimingDayOne_ResetsToDayOne()
    {
        UnlockEventOnMayFour();
        //eventActive = true
        //currentDayIndex = 0
        //claimedDays = [false, false, false]
        //CanClaim = true
        Assert.That(controller.TryClaim(), Is.True);

        time.Now = new DateTime(2026, 5, 6, 10, 0, 0);
        controller.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(controller.Status.CurrentDayIndex, Is.EqualTo(0));
            Assert.That(controller.Status.ClaimedDays, Is.All.False);
            Assert.That(controller.Status.CanClaim, Is.True);
        });
    }

    // Kiểm tra claim đủ Day 3 thì event đóng và bắt đầu cooldown 14 ngày.
    [Test]
    public void CompleteDayThree_ClosesEventAndStartsFourteenDayCooldown()
    {
        UnlockEventOnMayFour();
        //eventActive = true
        //currentDayIndex = 0
        //claimedDays = [false, false, false]
        //CanClaim = true
        Assert.That(controller.TryClaim(), Is.True);

        time.Now = new DateTime(2026, 5, 5, 8, 0, 0);
        controller.Refresh();
        Assert.That(controller.TryClaim(), Is.True);

        time.Now = new DateTime(2026, 5, 6, 8, 0, 0);
        controller.Refresh();
        Assert.That(controller.TryClaim(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(controller.Status.IsUnlocked, Is.False);
            Assert.That(controller.Status.CanClaim, Is.False);
            Assert.That(controller.Status.IsCompleted, Is.True);
            Assert.That(controller.Status.IsInCooldown, Is.True);
            Assert.That(controller.Status.CooldownUntilDate, Is.EqualTo(new DateTime(2026, 5, 20)));
        });

        time.Now = new DateTime(2026, 5, 7, 8, 0, 0);
        controller.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(controller.Status.IsUnlocked, Is.False);
            Assert.That(controller.Status.IsInCooldown, Is.True);
        });
    }

    // Kiểm tra save cũ thiếu field mới vẫn migrate được, không crash và giữ trạng thái hợp lệ.
    [Test]
    public void OldSaveMissingNewFields_MigratesWithoutCrashing()
    {
        string key = "ComebackBonus";
        var oldSave = MockSaveController.GetSaveObject<ComebackBonusSave>(key);
        oldSave.version = 0;
        oldSave.initialized = true;
        oldSave.lastLoginDate = new DateTime(2026, 5, 1);
        oldSave.lastKnownDate = new DateTime(2026, 5, 1);
        oldSave.claimedDays = null;

        time.Now = new DateTime(2026, 5, 4, 10, 0, 0);

        Assert.DoesNotThrow(() => controller.Init(config, time));
        Assert.Multiple(() =>
        {
            Assert.That(oldSave.version, Is.EqualTo(ComebackBonusSave.CurrentVersion));
            Assert.That(oldSave.claimedDays, Has.Length.EqualTo(config.TotalDays));
            Assert.That(controller.Status.IsUnlocked, Is.True);
            Assert.That(controller.Status.CanClaim, Is.True);
        });
    }

    // Kiểm tra claim thành công bắn event với dayIndex dạng 0-based để hệ thống khác hook vào.
    [Test]
    public void SuccessfulClaim_RaisesClaimedEventWithZeroBasedDayIndex()
    {
        var claimedIndexes = new List<int>();
        ComebackBonusController.OnComebackBonusClaimed += claimedIndexes.Add;
        UnlockEventOnMayFour();
        //eventActive = true
        //currentDayIndex = 0
        //claimedDays = [false, false, false]
        //CanClaim = true
        Assert.That(controller.TryClaim(), Is.True);

        Assert.That(claimedIndexes, Is.EqualTo(new[] { 0 }));
    }

    private void UnlockEventOnMayFour()
    {
        controller.Init(config, time);
        time.Now = new DateTime(2026, 5, 4, 9, 0, 0);
        controller.Refresh();
        Assert.That(controller.Status.CanClaim, Is.True);
    }

    private sealed class MockTimeProvider(DateTime now) : ITimeProvider
    {
        public DateTime Now { get; set; } = now;
    }
}
