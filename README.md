# Comeback Bonus Event Document

## Run test

Có thể chạy bằng Test Explorer trong Visual Studio
Khi debug, đặt breakpoint trong ComebackBonusController, ComebackBonusSave rồi chọn Debug test trong Test Explorer, mở bảng chọn autos để xem rõ flow thay đổi của các biến

Mình có vẽ UML flow mô tả luồng chạy, tên file là UMLFLow.png, đọc cùng với readme để hiểu hơn

## 1. Approach

Đề yêu cầu cần kiểm tra các rule của Comeback Bonus: khi nào mở event, khi nào claim được, khi nào reset, và save cũ có bị lỗi không. Vì vậy project hiện tại là C# class library kèm NUnit test.

Trong code, ComebackBonusConfig giữ config của event như số ngày vắng game cần thiết  cooldown và danh sách reward. ComebackBonusSave giữ data lưu theo player.
ComebackBonusStatus là trạng thái đã được build sẵn để UI đọc.
ComebackBonusController là nơi xử lý toàn bộ luồng chính.

## a. Luồng chính

Lần đầu player mở game sau khi có feature này, code chỉ init save. Lần đó không được tính là miss day, nên không tự bật Comeback Bonus ngay.

Sau đó mỗi lần vào game hoặc quay lại Home, controller gọi Refresh để tính lại trạng thái. Code lấy ngày bằng timeProvider.Now.Date, nên chỉ quan tâm ngày lịch, không quan tâm đủ 24h. Nếu player đã vắng game đủ 3 ngày thì event mở ở Day 1.

Khi claim thành công, controller đánh dấu ngày đó đã claim, lưu ngày claim gần nhất, rồi chuyển sang Day tiếp theo. Nếu claim xong Day 3 thì event đóng và bắt đầu cooldown 14 ngày

## b. Luồng code kiểm thử

SetUp trong test sẽ reset save, reset controller singleton và đặt thời gian ban đầu là 01/05. Sau đó mỗi test tạo controller mới để tránh state cũ ảnh hưởng sang test khác

Khi gọi Init, controller sẽ validate config, lấy save từ MockSaveController, chạy Migrate để sửa data cũ nếu cần, rồi gọi Refresh lần đầu.

Refresh chỉ là entry point để tính lại trạng thái hiện tại. Hàm này gọi Evaluate với thời gian từ timeProvider.

Trong Evaluate, luồng xử lý là:

- Nếu save mới chưa initialized thì init lần đầu, lưu lastLoginDate và lastKnownDate, nhưng không unlock event.
- Nếu phát hiện rollback time, tức today nhỏ hơn lastKnownDate, thì giữ progress hiện tại và không cho claim.
- Nếu event đang active thì kiểm tra người chơi có skip ngày trong chuỗi không.
- Nếu event chưa active thì kiểm tra player có miss đủ 3 ngày để mở event không.
- Cuối cùng build lại Status và NotifyChanged nếu cần.

Trong TryClaim, controller sẽ Evaluate lại trước để đảm bảo status mới nhất. Nếu Status.CanClaim là false thì return false. Nếu claim được, code claim reward, update claimedDays, lastClaimDate và currentDayIndex. Nếu đang claim ngày cuối thì đóng event và set cooldown 14 ngày. Sau đó controller build lại Status và bắn OnComebackBonusClaimed với dayIndex vừa claim để system khác như Missions hook vào.

Sau khi chạy xong logic test, nó sẽ chạy TearDown để reset lại đảm bảo không xung đột với các test chạy tuần tự sau

## c. UI popup đề xuất và Tracking(mô tả cho phần 5 PRD)

Mình không dựng UI thật trong bài này, nhưng ComebackBonusStatus đã có đủ data để bind ra popup ở Home.

Khi player vào màn Home, UI gọi Refresh rồi đọc Status. Nếu Status.CanClaim = true thì hiện popup Comeback Bonus. Nếu event chưa unlock, đang cooldown hoặc chưa tới ngày claim tiếp theo thì không cần bật popup.

Popup sẽ có 3 card xếp ngang: Day 1, Day 2, Day 3.

State của card map như sau:

- Claimed: Status.ClaimedDays[index] = true, card hiện check mark để đánh dấu ngày nhận
- Claimable: index == Status.CurrentDayIndex và Status.CanClaim = true, card hiện nút Claim.
- Locked: các ngày chưa tới hoặc chưa claim được, card bị disable

Khi bấm Claim, UI gọi TryClaim. Nếu trả true thì UI refresh lại popup để update card. Missions system có thể hook OnComebackBonusClaimed(dayIndex), với dayIndex là 0, 1 hoặc 2 tương ứng Day 1, Day 2, Day 3

## 2. Anti-cheat

Ngày mới được tính theo mốc 00:00 local. Ví dụ player claim Day 1 lúc 23:59 ngày 04/05, qua 00:01 ngày 05/05 thì đã là ngày mới và có thể claim Day 2 nếu chuỗi vẫn hợp lệ.

Mình có lưu lastKnownDate là ngày lớn nhất mà game từng thấy. Nếu hôm sau player chỉnh giờ máy lùi về quá khứ, today sẽ nhỏ hơn lastKnownDate. Khi đó code giữ nguyên progress cũ, nhưng không cho claim để tránh claim lại reward.

Với trường hợp chỉnh giờ tới hoặc bỏ lỡ ngày trong chuỗi, mình xử lý chung theo rule: Day tiếp theo chỉ hợp lệ nếu cách lần claim trước đúng 1 ngày calendar. Nếu khoảng cách lớn hơn 1 ngày thì xem như chain bị gãy và reset về Day 1. Cách này không cho player fast-forward để nhận nhanh Day 2 hoặc Day 3

## 3. Edge Cases

- OldSaveMissingNewFields_MigratesWithoutCrashing: save cũ thiếu data mới vẫn migrate được, không crash
- SuccessfulClaim_RaisesClaimedEventWithZeroBasedDayIndex: claim thành công thì bắn event cho system khác hook.
- Duplicate claim same day: sau khi claim thành công, claim lại trong cùng ngày bị chặn.
- Reward claim fail: nếu inventory/mailbox không nhận được reward thì không mark claimed và cho retry.
- Cooldown boundary: trong cooldown không trigger lại; sau cooldown vẫn phải miss đủ ngày mới mở lại.
- Corrupted old save: claimedDays sai size hoặc currentDayIndex vượt range sẽ được migrate/clamp an toàn.
- Rollback recovery: nếu detect chỉnh giờ lùi thì block claim, nhưng khi ngày máy quay lại hợp lệ thì progress cũ vẫn tiếp tục.

## 4. Inventory full

Trong bài test hiện tại, reward được claim qua IReward.TryClaim()
Nếu TryClaim trả false, controller sẽ không đánh dấu ngày đó đã claim, không tăng currentDayIndex và không bắn event claimed. Như vậy player không bị mất quà và có thể thử lại sau

Nếu đưa vào production mình sẽ xử lý inventory full bằng mailbox hoặc pending reward:

1. Thử add reward vào inventory
2. Nếu inventory đầy, gửi reward sang mailbox/pending reward
3. Chỉ mark claimed khi inventory hoặc mailbox lưu thành công.
4. Nếu cả hai đều fail, không mark claimed để player retry

## 5. Save migration

Vì game đã release production, player cũ có thể hoàn toàn chưa có data Comeback Bonus. Với case đó, initialized mặc định là false, nên lần đầu sau update chỉ được xem là lần init event. Code không trigger comeback ngay.

Migrate(totalDays) xử lý các save cũ bị thiếu claimedDays hoặc có claimedDays sai size. Nếu thiếu thì tạo mảng mới. Nếu có data cũ thì copy phần còn dùng được qua mảng mới. currentDayIndex cũng được clamp lại để không vượt khỏi số ngày reward hiện tại.

Nếu sau này thêm field mới, ví dụ lưu thời điểm claim chính xác tới giây, thì chỉ cần tăng version và set default an toàn trong Migrate. Save cũ không thể tự biết giây claim trong quá khứ nhưng ít nhất code mới vẫn load được, không crash và không phá progress cũ.
