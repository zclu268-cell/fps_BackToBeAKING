using UnityEngine;
using UnityEngine.UI;

namespace RoguePulse
{
    /// <summary>
    /// 在世界空间中显示浮动的伤害数字，自动上升并淡出后销毁。
    /// 通过 DamagePopup.Spawn() 静态方法创建实例。
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        [SerializeField] private float riseSpeed = 1.5f;
        [SerializeField] private float lifetime = 0.85f;

        private static readonly Color NormalColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color LargeHitColor = new Color(1f, 0.88f, 0.1f, 1f);

        // 超过此伤害值时显示为黄色大字
        private const float LargeHitThreshold = 30f;

        private Text _text;
        private Canvas _canvas;
        private Transform _cameraTransform;
        private float _dieAt;
        private Color _baseColor;

        private void Awake()
        {
            BuildUI();
        }

        private void Start()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
                _cameraTransform = mainCam.transform;

            _dieAt = Time.time + lifetime;
        }

        private void Update()
        {
            // 上升
            transform.position += Vector3.up * (riseSpeed * Time.deltaTime);

            // 朝向摄像机
            if (_cameraTransform != null && _canvas != null)
                _canvas.transform.forward = _cameraTransform.forward;

            // 淡出
            float progress = 1f - Mathf.Clamp01((_dieAt - Time.time) / lifetime);
            if (_text != null)
            {
                Color c = _baseColor;
                c.a = Mathf.Lerp(1f, 0f, progress * progress);
                _text.color = c;
            }

            if (Time.time >= _dieAt)
                Destroy(gameObject);
        }

        // ───────────────────────────── 构建UI ─────────────────────────────

        private void BuildUI()
        {
            GameObject canvasGo = new GameObject("PopupCanvas");
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 10;

            RectTransform canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(200f, 80f);
            canvasGo.transform.localScale = Vector3.one * 0.009f;

            // 数字文本
            GameObject textGo = new GameObject("DmgText");
            textGo.transform.SetParent(canvasGo.transform, false);
            _text = textGo.AddComponent<Text>();
            _text.alignment = TextAnchor.MiddleCenter;
            _text.fontSize = 56;
            _text.fontStyle = FontStyle.Bold;

            RectTransform textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        // ───────────────────────────── 公开接口 ─────────────────────────────

        /// <summary>初始化弹出数字内容与颜色。</summary>
        public void Setup(float damage)
        {
            bool isLargeHit = damage >= LargeHitThreshold;
            _baseColor = isLargeHit ? LargeHitColor : NormalColor;

            if (_text != null)
            {
                _text.text = Mathf.RoundToInt(damage).ToString();
                _text.color = _baseColor;
                _text.fontSize = isLargeHit ? 72 : 56;
            }
        }

        /// <summary>
        /// 在指定世界坐标生成一个伤害数字弹出。
        /// </summary>
        /// <param name="worldPosition">生成位置（建议传入野怪头顶位置）</param>
        /// <param name="damage">伤害值</param>
        public static DamagePopup Spawn(Vector3 worldPosition, float damage)
        {
            // 轻微随机偏移，避免多次伤害重叠
            Vector3 spawnPos = worldPosition + new Vector3(
                Random.Range(-0.25f, 0.25f),
                Random.Range(0f, 0.4f),
                0f);

            GameObject go = new GameObject("DamagePopup");
            go.transform.position = spawnPos;
            DamagePopup popup = go.AddComponent<DamagePopup>();
            popup.Setup(damage);
            return popup;
        }
    }
}
