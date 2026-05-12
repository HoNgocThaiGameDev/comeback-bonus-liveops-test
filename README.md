# Comeback Bonus

## Approach

Project này mình implement Comeback Bonus bằng C# class library kèm NUnit test. Logic chính nằm trong ComebackBonusController. UI có thể đọc trạng thái từ snapshot ComebackBonusStatus và listen event để bind popup.

Save state nằm trong ComebackBonusSave, gồm các biến version, initialized, eventActive, completed, currentDayIndex, lastLoginDate, lastKnownDate, lastClaimDate, cooldownUntilDate và claimedDays

Mình có vẽ UML Flow, tên file là UMLFlow.png để hiểu rõ luồng code

Lần đầu player mở game sau khi có feature này, biến initialized đang false nên code chỉ init save, lưu ngày login đầu tiên và không trigger event. Sau đó nếu player vắng đủ 3 ngày lịch thì event unlock ở Day 1. Khi claim thành công, code lưu ngày đã claim, tăng biến currentDayIndex. Nếu claim Day 3 thì đóng event và set cooldown 14 ngày

## Anti-cheat

Code dùng ngày từ timeProvider.Now.Date, nên ngày mới được tính theo mốc 00:00 local, không cần chờ đủ 24 giờ.

Để detect chỉnh giờ lùi, save lưu biến lastKnownDate là ngày lớn nhất game từng thấy. Nếu ngày hiện tại nhỏ hơn biến lastKnownDate, code giữ progress cũ nhưng không cho claim để tránh claim lại reward.

Với chỉnh giờ tới hoặc skip ngày trong chuỗi, Day tiếp theo chỉ được claim nếu cách biến lastClaimDate đúng 1 ngày calendar. Nếu khoảng cách lớn hơn 1 ngày, chain bị xem là gãy và reset về Day 1.

## Edge Cases

- Save cũ thiếu data mới: migrate được, không crash.
- Claim thành công: bắn event claimed với dayIndex dạng 0, 1, 2 để hệ thống khác hook.
- Duplicate claim same day: sau khi claim thành công, claim lại trong cùng ngày bị chặn.
- Reward claim fail: nếu inventory/mailbox không nhận được reward thì không mark claimed và cho retry
- Cooldown boundary: trong cooldown không trigger lại; sau cooldown vẫn phải miss đủ ngày mới mở lại.
- Corrupted old save: biến claimedDays sai size hoặc biến currentDayIndex vượt range sẽ được migrate/clamp an toàn.
- Rollback recovery: nếu detect chỉnh giờ lùi thì block claim, nhưng khi ngày máy quay lại hợp lệ thì progress cũ vẫn tiếp tục
- Fast-forward từng ngày một: local time không phân biệt chắc được player quay lại thật mỗi ngày hay cheat chỉnh ngày từng bước; production nên dùng server timestamp hoặc trusted time để validate claim

## Inventory full

Trong bài test hiện tại, reward được claim qua hàm TryClaim trong interface IReward. Nếu hàm TryClaim trả false, controller không đánh dấu ngày đó đã claim, không tăng biến currentDayIndex và không bắn event claimed. Như vậy player không bị mất quà và có thể thử lại sau.

Nếu đưa vào production, mình sẽ xử lý inventory full bằng mailbox hoặc pending reward. Chỉ mark claimed khi inventory hoặc mailbox lưu reward thành công. Nếu cả hai đều fail, giữ nguyên progress để player retry

## Save migration

Hàm Migrate trong ComebackBonusSave xử lý save cũ trước khi controller dùng data. Nếu biến claimedDays bị null hoặc sai size, code tạo mảng mới và copy phần dữ liệu cũ còn hợp lệ. Biến currentDayIndex cũng được clamp lại để không vượt khỏi số ngày reward hiện tại.

Nếu sau này thêm field mới, ví dụ claim timestamp chính xác tới giây, mình sẽ tăng save version và set default an toàn trong hàm Migrate. Save cũ vẫn load được, không crash và không làm mất progress đã có
