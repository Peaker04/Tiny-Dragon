# Thiết kế màn Level 03 – Boss & Mảnh Ngọc Rồng

## 1. Mục tiêu thiết kế

Level 03 được thiết kế như một màn **boss fight có cơ chế phá giáp**. Người chơi sẽ gặp boss ngay từ đầu, nhưng chưa thể gây sát thương thật cho boss cho đến khi thu thập đủ **2 mảnh vỡ ngọc rồng** trên các đảo bay.

Ý tưởng chính:

```text
Boss xuất hiện ngay khi vào màn
↓
Người chơi đánh boss nhưng boss chỉ mất 1 HP hoặc gần như không mất máu
↓
Người chơi phải leo lên các đảo bay để lấy 2 mảnh ngọc rồng
↓
Các đảo bay ẩn / hiện mỗi lần người chơi nhấn Jump
↓
Mảnh ngọc rồng di chuyển theo quy luật cố định
↓
Nhặt đủ 2/2 mảnh
↓
Hai mảnh ghép thành viên ngọc hoàn chỉnh
↓
Boss bị phá giáp
↓
Người chơi đánh boss mất máu thật
```

---

## 2. Luồng gameplay tổng thể

### Giai đoạn 1: Boss xuất hiện ngay từ đầu

Khi người chơi vào Level 03:

- Boss đã đứng sẵn ở bên phải màn hình.
- Boss có thể đi qua lại hoặc tấn công nhẹ nếu người chơi lại gần.
- Người chơi có thể đánh boss, nhưng boss chưa mất máu thật.
- Mỗi lần đánh trúng boss trong giai đoạn này, boss chỉ mất **1 HP tượng trưng** hoặc hiện hiệu ứng **Shield / Immune**.

Mục tiêu của giai đoạn này là cho người chơi hiểu rằng:

> Muốn đánh boss thật thì phải phá giáp boss trước.

---

### Giai đoạn 2: Thu thập mảnh ngọc rồng

Người chơi phải nhảy lên các đảo bay ở giữa màn để lấy 2 mảnh vỡ ngọc rồng.

Các đảo bay lấy từ block đã có ở Level 02, nhưng nên thu nhỏ chiều ngang để phù hợp với màn boss.

Đề xuất scale đảo:

```text
Scale X: 0.45 - 0.6 so với đảo ở Level 02
Scale Y: 0.85 - 1.0
```

Không nên làm đảo quá nhỏ vì nhân vật nhỏ, nếu đảo quá hẹp sẽ khiến màn chơi khó chịu.

---

### Giai đoạn 3: Ghép ngọc rồng

Khi người chơi thu thập đủ:

```text
Dragon Fragment: 2 / 2
```

Hai mảnh ngọc sẽ:

1. Bay về một điểm giữa màn hình hoặc phía trên boss.
2. Xoay quanh nhau.
3. Ghép lại thành 1 viên ngọc hoàn chỉnh.
4. Phát hiệu ứng ánh sáng.
5. Boss bị phá giáp.

Sau hiệu ứng này, boss chuyển sang trạng thái có thể bị đánh mất máu thật.

---

## 3. Bố cục màn đề xuất

Dựa trên bố cục hiện tại của Level 03, nên đặt boss ở bên phải, player bắt đầu bên trái, các đảo bay nằm ở khu vực giữa màn.

Sơ đồ tổng quát:

```text
[Player Start]                                      [Boss]

        Fragment 1                    Fragment 2
            ▲                              ▲

      Platform A2        Platform B2      Platform A3

             Platform B1        Platform A1

------------------------------------------------------------
Ground
```

Giải thích:

- Player bắt đầu ở bên trái dưới đất.
- Boss đứng bên phải dưới đất.
- Đảo bay nằm ở giữa và cao hơn mặt đất.
- 2 mảnh ngọc nằm ở trên cao, bắt buộc người chơi phải dùng đảo bay để lấy.

---

## 4. Cơ chế đảo bay ẩn / hiện

Các đảo bay được chia thành 2 nhóm:

```text
PlatformGroup_A: hiện ban đầu
PlatformGroup_B: ẩn ban đầu
```

Mỗi lần người chơi nhấn nút Jump:

```text
Nếu Group A đang hiện:
    Group A ẩn
    Group B hiện

Nếu Group B đang hiện:
    Group B ẩn
    Group A hiện
```

Tuy nhiên, đảo không nên biến mất ngay lập tức. Nên có delay nhỏ để người chơi không bị rơi một cách bất công.

Đề xuất timing:

```text
Người chơi nhấn Jump
↓
0.15 giây sau đảo cũ bắt đầu mờ
↓
0.25 giây sau đảo cũ tắt collider
↓
Đảo mới bật collider và hiện rõ
```

Hiệu ứng đảo:

```text
Đảo đang hiện:
    Hiện rõ → mờ dần → tắt collider → biến mất

Đảo mới:
    Mờ → hiện rõ → bật collider
```

---

## 5. Quy luật di chuyển mảnh ngọc rồng

Mảnh ngọc không nên di chuyển random. Nếu random, người chơi sẽ cảm thấy màn chơi thiếu công bằng.

Nên cho mảnh ngọc di chuyển theo **số lần người chơi nhấn Jump** hoặc theo **số lần đảo đổi trạng thái**.

Công thức đơn giản:

```text
Mỗi lần người chơi nhấn Jump
→ Đảo đổi trạng thái
→ Mảnh ngọc chuyển sang vị trí tiếp theo trong danh sách
```

---

## 6. Quy luật cho mảnh ngọc 1

Mảnh ngọc 1 nên dễ hơn để người chơi học cơ chế.

Quy luật đề xuất:

```text
Step 0: Fragment 1 nằm trên Platform A1
Step 1: Fragment 1 bay sang vị trí gần Platform B1
Step 2: Fragment 1 bay lên vị trí gần Platform A2
Step 3: Fragment 1 đứng yên để người chơi lấy
```

Đường đi của người chơi:

```text
Ground
→ Nhảy lên Platform A1
→ Nhấn Jump, Group A ẩn, Group B hiện
→ Nhảy sang Platform B1
→ Mảnh 1 bay lên vị trí cao hơn
→ Nhấn Jump tiếp, Group A hiện lại
→ Nhảy lên Platform A2
→ Lấy Fragment 1
```

Mục tiêu của mảnh 1:

- Dạy người chơi hiểu luật đảo ẩn / hiện.
- Không nên đặt quá khó.
- Không nên để gần boss quá sớm.

---

## 7. Quy luật cho mảnh ngọc 2

Mảnh ngọc 2 có thể khó hơn một chút vì lúc này người chơi đã hiểu cơ chế.

Quy luật đề xuất:

```text
Step 0: Fragment 2 nằm trên Platform B2
Step 1: Fragment 2 bay sang gần Platform A3
Step 2: Fragment 2 bay xuống gần Platform B3 hoặc vị trí trung gian
Step 3: Fragment 2 đứng yên để người chơi lấy
```

Đường đi của người chơi:

```text
Từ Platform A2 hoặc vị trí giữa màn
→ Nhấn Jump để đổi nhóm đảo
→ Nhảy sang Platform B2
→ Fragment 2 chuyển sang phía phải
→ Nhấn Jump để đổi nhóm đảo
→ Nhảy sang Platform A3
→ Lấy Fragment 2
```

Mục tiêu của mảnh 2:

- Tăng độ khó vừa phải.
- Bắt người chơi dùng thành thạo cơ chế đổi đảo.
- Tạo cảm giác căng thẳng trước khi phá giáp boss.

---

## 8. Vị trí object đề xuất trong Unity

Có thể dùng vị trí tham khảo sau, sau đó chỉnh lại theo camera và lực nhảy của player.

```text
Player Start:      X = -7.0, Y = -3.0
Boss:              X =  4.5, Y = -3.0

Platform A1:       X = -3.5, Y = -1.6
Platform B1:       X = -1.5, Y = -0.6
Platform A2:       X =  0.5, Y =  0.5
Platform B2:       X =  2.5, Y = -0.4
Platform A3:       X =  4.0, Y =  0.8

Fragment 1 Final:  X =  0.5, Y =  1.2
Fragment 2 Final:  X =  4.0, Y =  1.5
```

Nếu player nhảy không tới:

- Giảm khoảng cách X giữa các đảo.
- Hạ thấp Y của đảo.
- Tăng nhẹ lực nhảy của player.
- Tăng chiều ngang đảo một chút.

---

## 9. Boss trong trạng thái chưa phá giáp

Trong lúc chưa đủ 2 mảnh ngọc, boss nên hoạt động đơn giản.

Hành vi đề xuất:

```text
Boss đứng dưới đất
Boss đi qua lại nhẹ
Boss chém nếu player lại gần
Boss không mất máu thật
Boss hiện hiệu ứng Shielded khi bị đánh
```

Logic sát thương:

```text
Nếu chưa đủ 2 mảnh:
    Boss chỉ mất 1 HP tượng trưng hoặc không mất máu
    Hiện chữ "Shielded!"
    Phát hiệu ứng khiên

Nếu đã đủ 2 mảnh và ngọc đã ghép xong:
    Boss mất máu bình thường
```

---

## 10. Boss sau khi bị phá giáp

Sau khi ngọc rồng hoàn chỉnh xuất hiện, boss chuyển sang trạng thái yếu hơn hoặc nổi giận.

Có 2 hướng thiết kế:

### Cách 1: Boss yếu đi

Phù hợp nếu bạn muốn màn dễ hơn.

```text
Boss mất shield
Boss nhận sát thương bình thường
Boss giữ nguyên tốc độ đánh
```

### Cách 2: Boss chuyển phase

Phù hợp nếu bạn muốn màn có cảm giác boss fight rõ hơn.

```text
Boss mất shield
Boss gầm lên
Boss tăng tốc nhẹ
Boss đánh nhanh hơn
Boss có thêm hiệu ứng khi chém
```

Đề xuất dùng cách 2 nhưng tăng nhẹ thôi, không nên làm quá khó.

---

## 11. Hiệu ứng ghép ngọc rồng

Khi nhặt đủ 2/2 mảnh, nên có hiệu ứng rõ ràng để người chơi hiểu boss đã bị phá giáp.

Chuỗi hiệu ứng đề xuất:

```text
Player nhặt Fragment 2
↓
Game pause nhẹ 0.2 - 0.3 giây
↓
Fragment 1 và Fragment 2 bay về điểm giữa màn
↓
Hai mảnh xoay quanh nhau
↓
Ghép thành Dragon Gem hoàn chỉnh
↓
Phát sáng
↓
Camera shake nhẹ
↓
Boss shield vỡ
↓
Hiện text: Boss Shield Broken!
```

Vị trí ghép ngọc:

```text
X = 0 hoặc gần giữa camera
Y = 1.5 đến 2.0
```

Hoặc có thể ghép ngay phía trên boss để người chơi thấy boss bị ảnh hưởng trực tiếp.

---

## 12. State quản lý Level 03

Nên dùng enum để quản lý trạng thái màn chơi.

```csharp
public enum Level03State
{
    BossShielded,
    CollectingFragments,
    DragonGemMerging,
    BossVulnerable,
    BossDefeated
}
```

Luồng state:

```text
Start Level
→ BossShielded
→ CollectingFragments
→ Đủ 2 mảnh
→ DragonGemMerging
→ BossVulnerable
→ BossDefeated
```

---

## 13. Cấu trúc Hierarchy đề xuất

Nên sắp xếp object trong Unity như sau để dễ quản lý.

```text
Level_03
 ├── Player
 ├── Boss
 ├── Ground
 ├── FlyingPlatforms
 │    ├── PlatformGroup_A
 │    │    ├── Platform_A1
 │    │    ├── Platform_A2
 │    │    └── Platform_A3
 │    └── PlatformGroup_B
 │         ├── Platform_B1
 │         ├── Platform_B2
 │         └── Platform_B3
 ├── DragonFragments
 │    ├── Fragment_01
 │    └── Fragment_02
 ├── DragonGemComplete
 ├── Effects
 │    ├── ShieldEffect
 │    ├── MergeEffect
 │    └── BreakShieldEffect
 └── Level03Manager
```

---

## 14. Biến cần có trong Level03Manager

```csharp
public class Level03Manager : MonoBehaviour
{
    public Level03State currentState;

    public int collectedFragments = 0;
    public int requiredFragments = 2;

    public GameObject platformGroupA;
    public GameObject platformGroupB;

    public GameObject fragment1;
    public GameObject fragment2;
    public GameObject dragonGemComplete;

    public BossHealth bossHealth;

    private bool isGroupAActive = true;
}
```

---

## 15. Pseudo logic đổi đảo khi Jump

```csharp
public void OnPlayerJump()
{
    if (currentState != Level03State.CollectingFragments &&
        currentState != Level03State.BossShielded)
    {
        return;
    }

    TogglePlatforms();
    MoveFragmentsToNextStep();
}

private void TogglePlatforms()
{
    isGroupAActive = !isGroupAActive;

    platformGroupA.SetActive(isGroupAActive);
    platformGroupB.SetActive(!isGroupAActive);
}
```

Về sau có thể thay `SetActive` bằng animation mờ dần để đẹp hơn.

---

## 16. Pseudo logic boss nhận damage

```csharp
public void TakeDamage(int damage)
{
    if (!canTakeRealDamage)
    {
        currentHp -= 1;
        ShowShieldEffect();
        return;
    }

    currentHp -= damage;

    if (currentHp <= 0)
    {
        Die();
    }
}
```

Hoặc nếu không muốn boss mất máu trước khi phá giáp:

```csharp
public void TakeDamage(int damage)
{
    if (!canTakeRealDamage)
    {
        ShowShieldEffect();
        return;
    }

    currentHp -= damage;
}
```

---

## 17. Pseudo logic nhặt mảnh ngọc

```csharp
public void CollectFragment(GameObject fragment)
{
    fragment.SetActive(false);
    collectedFragments++;

    if (collectedFragments >= requiredFragments)
    {
        StartCoroutine(MergeDragonGem());
    }
}
```

---

## 18. Pseudo logic ghép ngọc

```csharp
private IEnumerator MergeDragonGem()
{
    currentState = Level03State.DragonGemMerging;

    // Khóa điều khiển player trong thời gian ngắn nếu cần
    yield return new WaitForSeconds(0.3f);

    // Chạy animation 2 mảnh bay vào giữa
    // Chạy hiệu ứng xoay và phát sáng
    yield return new WaitForSeconds(1.0f);

    dragonGemComplete.SetActive(true);

    // Phá giáp boss
    bossHealth.EnableRealDamage();

    currentState = Level03State.BossVulnerable;
}
```

---

## 19. Quy tắc cân bằng độ khó

Để màn chơi không bị quá khó chịu, nên tuân theo các quy tắc sau:

### Không reset mảnh ngọc khi người chơi rơi xuống đất

Nếu player rơi xuống đất, chỉ cho player leo lại.

Không nên reset toàn bộ mảnh ngọc vì sẽ làm màn chơi rất bực.

---

### Đảo chỉ đổi khi người chơi nhấn Jump

Không nên cho đảo tự động đổi theo thời gian ngay từ đầu.

Lý do:

- Người chơi dễ hiểu cơ chế hơn.
- Người chơi cảm thấy mình kiểm soát được màn chơi.
- Giảm cảm giác bất công.

---

### Mảnh ngọc phải di chuyển theo quy luật cố định

Không dùng random.

Nên dùng danh sách điểm cố định:

```text
Fragment 1 Path Points:
P1 → P2 → P3 → Final

Fragment 2 Path Points:
P1 → P2 → P3 → Final
```

---

### Không đặt mảnh ngọc quá gần boss

Nếu mảnh ngọc nằm quá gần boss, người chơi sẽ vừa phải platforming vừa né boss, dễ bị rối.

Nên để mảnh ngọc ở khu giữa hoặc phía trên, còn boss ở dưới bên phải.

---

## 20. Kết luận thiết kế

Level 03 nên là một màn boss có cơ chế phá giáp bằng ngọc rồng.

Tóm tắt gameplay:

```text
Vào màn gặp boss
↓
Đánh boss chưa hiệu quả
↓
Leo đảo bay để lấy mảnh ngọc
↓
Đảo ẩn / hiện mỗi lần nhấn Jump
↓
Mảnh ngọc di chuyển theo step cố định
↓
Nhặt đủ 2 mảnh
↓
Ghép thành viên ngọc hoàn chỉnh
↓
Boss mất shield
↓
Đánh boss mất máu thật
↓
Hạ boss qua màn
```

Thiết kế này giúp Level 03 khác Level 02 vì không chỉ có platforming, mà còn có mục tiêu boss fight rõ ràng. Đồng thời vẫn tận dụng lại block đảo bay đã có ở Level 02, chỉ cần chỉnh scale và thêm cơ chế ẩn / hiện.
