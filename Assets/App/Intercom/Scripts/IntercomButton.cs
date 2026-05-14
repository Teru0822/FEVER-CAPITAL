using UnityEngine;
using UnityEngine.Events;

namespace Assets.App.Intercom.Scripts
{
    /// <summary>
    /// インターホンの物理ボタンをマウスでクリックした際のイベントを管理するクラス。
    /// オブジェクトに Collider が付いている必要があります。
    /// </summary>
    public class IntercomButton : MonoBehaviour
    {
        [SerializeField] private UnityEvent onClick;

        // マウスクリック時に UnityEvent を発火
        // （BarControllerのようなRaycast方式で呼び出すことも可能）
        public void OnClick()
        {
            onClick?.Invoke();
        }

        // シンプルな動作確認用に OnMouseDown もサポート (Collider必須)
        private void OnMouseDown()
        {
            OnClick();
        }
    }
}
