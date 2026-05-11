// ============================================
// Stubs.cs - Stub interfaces (KHÔNG sửa)
// Cung cấp cho bài test LiveOps Event Dev
// Mock các hệ thống nội bộ (SaveController, EventBus, Reward, ...)
// ============================================
using System;
using System.Collections.Generic;

namespace LiveOpsTest.Stubs
{
    // ========================================
    // SAVE SYSTEM
    // ========================================

    /// <summary>Mọi save object phải implement interface này.</summary>
    public interface ISaveObject
    {
        /// <summary>Pre-save hook - chuẩn bị data trước khi flush ra disk.</summary>
        void Flush();
    }

    /// <summary>In-memory save controller cho test.</summary>
    public static class MockSaveController
    {
        private static readonly Dictionary<string, ISaveObject> store = new();

        /// <summary>True nếu có change cần persist.</summary>
        public static bool IsDirty { get; private set; }

        /// <summary>Lấy save object theo key. Tạo mới nếu chưa có.</summary>
        public static T GetSaveObject<T>(string key) where T : class, ISaveObject, new()
        {
            if (!store.TryGetValue(key, out var s))
            {
                s = new T();
                store[key] = s;
            }
            return (T)s;
        }

        /// <summary>Đánh dấu cần save - hệ thống sẽ flush ra disk ở thời điểm thích hợp.</summary>
        public static void MarkAsSaveIsRequired() => IsDirty = true;

        /// <summary>Reset toàn bộ store (dùng cho test).</summary>
        public static void Reset() { store.Clear(); IsDirty = false; }
    }

    // ========================================
    // REWARD SYSTEM (POLYMORPHIC)
    // ========================================

    /// <summary>Polymorphic reward interface - hỗ trợ nhiều loại quà (Coin/Booster/XP/...).</summary>
    public interface IReward
    {
        /// <summary>Validate config (vd: amount > 0).</summary>
        bool IsValid();

        /// <summary>Apply reward vào ví/inventory. Return false nếu không thể claim (vd: inventory full).</summary>
        bool TryClaim();
    }

    [Serializable]
    public class CoinReward : IReward
    {
        public int amount;
        public bool IsValid() => amount > 0;
        public bool TryClaim()
        {
            // TODO: Add to wallet via CurrencyController
            return true;
        }
    }

    [Serializable]
    public class BoosterReward : IReward
    {
        public string boosterId;
        public int quantity;
        public bool IsValid() => !string.IsNullOrEmpty(boosterId) && quantity > 0;
        public bool TryClaim()
        {
            // TODO: Add to inventory; return false nếu inventory full
            return true;
        }
    }

    // ========================================
    // TIME PROVIDER (ĐỂ TEST CHEAT DETECTION)
    // ========================================

    /// <summary>
    /// Time provider abstraction - inject để test cheat detection.
    /// HINT: Trong unit tests, tạo MockTimeProvider để set Now thủ công, mô phỏng chỉnh giờ tới/lùi.
    /// </summary>
    public interface ITimeProvider
    {
        DateTime Now { get; }
    }

    /// <summary>Default implementation - dùng giờ hệ thống.</summary>
    public class SystemTimeProvider : ITimeProvider
    {
        public DateTime Now => DateTime.Now;
    }
}
