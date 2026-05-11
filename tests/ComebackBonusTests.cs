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
