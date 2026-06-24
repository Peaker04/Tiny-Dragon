using UnityEngine;
using UnityEngine.EventSystems;

namespace TinyDragon.UI
{
    public class PersistentEventSystem : MonoBehaviour
    {
        private static PersistentEventSystem instance;

        private void Awake()
        {
            // Kiểm tra xem đã có EventSystem nào đang tồn tại chưa
            if (instance != null && instance != this)
            {
                // Tiêu diệt EventSystem bị trùng lặp ở màn mới
                Destroy(gameObject);
                return;
            }

            // Đánh dấu đây là EventSystem chính thức và giữ nó sống sót qua mọi màn
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
