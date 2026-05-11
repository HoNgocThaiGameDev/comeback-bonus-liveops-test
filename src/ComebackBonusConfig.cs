using LiveOpsTest.Stubs;

namespace LiveOpsTest;

public sealed class ComebackBonusConfig
{
    // Luat trong PRD: player phai vang game it nhat 3 ngay lich thi moi unlock event
    public const int DefaultRequiredMissDays = 3;

    // Luat trong PRD: sau khi claim xong Day 3, event cooldown 14 ngay.
    public const int DefaultCooldownDays = 14;

    // So ngay vang game can co truoc khi Comeback Bonus duoc kich hoat
    public int RequiredMissDays { get; init; } = DefaultRequiredMissDays;

    // So ngay cooldown sau khi player hoan thanh chuoi 3 ngay.
    public int CooldownDays { get; init; } = DefaultCooldownDays;

    // Danh sach reward theo index bat dau tu 0: index 0 = Day 1, index 1 = Day 2.
    public IReward[] Rewards { get; init; } =
    [
        new CoinReward { amount = 100 },
        new CoinReward { amount = 300 },
        new BoosterReward { boosterId = "hint_plus", quantity = 1 }
    ];

    // Tong so ngay cua event lay theo so luong reward de config va logic luon khop nhau
    public int TotalDays => Rewards.Length;

    public void Validate()
    {
        if (RequiredMissDays <= 0)
        {
            throw new InvalidOperationException("Required miss days must be greater than zero.");
        }

        if (CooldownDays < 0)
        {
            throw new InvalidOperationException("Cooldown days cannot be negative.");
        }

        if (Rewards.Length == 0)
        {
            throw new InvalidOperationException("Comeback Bonus must have at least one reward.");
        }

        if (Rewards.Any(reward => reward == null || !reward.IsValid()))
        {
            throw new InvalidOperationException("Comeback Bonus contains an invalid reward.");
        }
    }
}
