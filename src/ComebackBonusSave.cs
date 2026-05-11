using LiveOpsTest.Stubs;

namespace LiveOpsTest;

[Serializable]
public sealed class ComebackBonusSave : ISaveObject
{
    // Version cua cau truc save. Tang so nay khi them field moi can migrate
    public const int CurrentVersion = 1;

    // Version hien dang luu trong save cua player.
    public int version = CurrentVersion;

    // False khi player chua tung duoc khoi tao data Comeback Bonus.
    public bool initialized;

    // True khi event dang active va chuoi 3 ngay dang chay.
    public bool eventActive;

    // True sau khi player claim reward cuoi cung va event vao cooldown
    public bool completed;

    // Ngay hien tai dang cho claim, dem tu 0: 0 = Day 1, 1 = Day 2, 2 = Day 3
    public int currentDayIndex;

    // Ngay lich local gan nhat ma player mo game.
    public DateTime lastLoginDate;

    // Ngay lich local lon nhat tung thay, dung de detect player chinh gio lui.
    public DateTime lastKnownDate;

    // Ngay lich local cua lan claim thanh cong gan nhat
    public DateTime lastClaimDate;

    //ngay sớm nhất mà event được trigger lại
    public DateTime cooldownUntilDate;

    // Trang thai da claim tung ngay. Idex 0 = Day 1
    public bool[]? claimedDays;

    public void Migrate(int totalDays)
    {
        if (version <= 0)
        {
            version = CurrentVersion;
        }

        if (claimedDays == null || claimedDays.Length != totalDays)// save cũ chưa có mảng, mảng cũ nhưng size sai
        {
            var migrated = new bool[totalDays];
            if (claimedDays != null)// có data cũ để copy k, co thi vao
            {
                //shallow copy
                Array.Copy(claimedDays, migrated, Math.Min(claimedDays.Length, migrated.Length));
            }

            claimedDays = migrated;
        }

        currentDayIndex = Math.Clamp(currentDayIndex, 0, Math.Max(0, totalDays - 1));
    }

    public void Flush()
    {
        if (claimedDays == null)
        {
            claimedDays = [];
        }
    }
}
