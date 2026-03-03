using UnityEngine;
using UnityEngine.UI;

namespace RoguePulse
{
    /// <summary>
    /// 在野怪头顶显示世界空间血量条，并在受伤时弹出伤害数字。
    /// 由 SpawnDirector 在运行时自动添加，无需手动挂载。
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class EnemyHealthBar : MonoBehaviour
    {
        [Header("位置与尺寸")]
        [SerializeField] private float heightOffset = 0.4f;   // 在包围盒顶部额外偏移
        [SerializeField] private float barWidth  = 1.4f;
        [SerializeField] private float barHeight = 0.18f;

        [Header("颜色")]
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        [SerializeField] private Color healthyColor    = new Color(0.20f, 0.80f, 0.25f, 1f);
        [SerializeField] private Color lowHpColor      = new Color(0.88f, 0.18f, 0.12f, 1f);
        [SerializeField, Range(0f, 1f)] private float lowHpThreshold = 0.3f;

        [Header("伤害数字")]
        [SerializeField] private bool showDamageNumbers = true;

        private Damageable  _damageable;
        private GameObject  _canvasGo;
        private RectTransform _fillRect;   // 用 anchorMax.x 控制填充，无需 Sprite
        private Image         _fillImage;
        private Transform     _camTransform;
        private float         _previousHp;
        private float         _barWorldY;   // 血条在世界空间的 Y 位置

        // 画布坐标系：100 画布单位 = 1 世界单位
        private const float PPU = 100f;

        // ─────────────────────── 生命周期 ───────────────────────

        private void Awake()
        {
            _damageable = GetComponent<Damageable>();
            _barWorldY  = CalcBarHeight();
            BuildCanvas();
        }

        private void OnEnable()
        {
            if (_damageable != null)
                _damageable.OnHealthChanged += OnHealthChanged;
        }

        private void OnDisable()
        {
            if (_damageable != null)
                _damageable.OnHealthChanged -= OnHealthChanged;
        }

        private void Start()
        {
            Camera cam = Camera.main;
            if (cam != null) _camTransform = cam.transform;

            _previousHp = _damageable.CurrentHp;
            RefreshFill(_damageable.CurrentHp, _damageable.MaxHp);
        }

        private void LateUpdate()
        {
            if (_canvasGo == null) return;

            // 保持血条始终在野怪正上方（世界坐标）
            Vector3 pos = transform.position;
            pos.y = _barWorldY;
            _canvasGo.transform.position = pos;

            // Billboard：朝向摄像机
            if (_camTransform != null)
                _canvasGo.transform.forward = _camTransform.forward;
        }

        // ─────────────────────── 构建 Canvas ───────────────────────

        private void BuildCanvas()
        {
            // ── 根节点（不作为子物体，避免随敌人旋转倾斜）──
            _canvasGo = new GameObject("EnemyHPCanvas");
            _canvasGo.transform.position = transform.position + Vector3.up * _barWorldY;

            Canvas canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode     = RenderMode.WorldSpace;
            canvas.overrideSorting = true;          // 覆盖默认排序
            canvas.sortingOrder   = 100;            // 确保在3D物体之上渲染

            RectTransform canvasRect = (RectTransform)_canvasGo.transform;
            canvasRect.sizeDelta  = new Vector2(barWidth * PPU, barHeight * PPU);
            _canvasGo.transform.localScale = Vector3.one / PPU;

            // ── 背景（深色，不透明，确保可见）──
            CreateImage(_canvasGo.transform, "BG", backgroundColor, stretch: true, inset: 0f, out _);

            // ── 填充外框（留 4px 内边距）──
            GameObject container = new GameObject("FillContainer", typeof(RectTransform));
            container.transform.SetParent(_canvasGo.transform, false);
            RectTransform cRect = (RectTransform)container.transform;
            cRect.anchorMin = Vector2.zero;
            cRect.anchorMax = Vector2.one;
            cRect.offsetMin = new Vector2(4f, 4f);
            cRect.offsetMax = new Vector2(-4f, -4f);

            // ── 填充条（anchor 方式，不依赖 Sprite）──
            GameObject fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(container.transform, false);
            _fillImage = fillGo.AddComponent<Image>();
            _fillImage.color = healthyColor;

            _fillRect = (RectTransform)fillGo.transform;
            _fillRect.anchorMin = new Vector2(0f, 0f);
            _fillRect.anchorMax = new Vector2(1f, 1f);  // 满血时铺满
            _fillRect.offsetMin = Vector2.zero;
            _fillRect.offsetMax = Vector2.zero;
        }

        private static void CreateImage(Transform parent, string name, Color color,
                                        bool stretch, float inset, out Image img)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            img = go.AddComponent<Image>();
            img.color = color;
            if (stretch)
            {
                RectTransform rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(inset, inset);
                rt.offsetMax = new Vector2(-inset, -inset);
            }
        }

        // ─────────────────────── 高度计算 ───────────────────────

        /// <summary>根据碰撞体/渲染器计算合适的血条 Y 位置（世界单位）。</summary>
        private float CalcBarHeight()
        {
            // 优先使用 CapsuleCollider（多数人形敌人）
            CapsuleCollider cap = GetComponent<CapsuleCollider>();
            if (cap != null)
                return cap.center.y + cap.height * 0.5f * transform.localScale.y + heightOffset;

            // 其次使用 CharacterController
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
                return cc.height * transform.localScale.y + heightOffset;

            // 再次使用 BoxCollider
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
                return box.center.y + box.size.y * 0.5f * transform.localScale.y + heightOffset;

            // 最后尝试 Renderer Bounds
            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend != null)
                return rend.bounds.extents.y * 2f + heightOffset;

            // 兜底固定值
            return 2.2f + heightOffset;
        }

        // ─────────────────────── 事件处理 ───────────────────────

        private void OnHealthChanged(Damageable d, float current, float max)
        {
            // 弹出伤害数字
            if (showDamageNumbers)
            {
                float dmg = _previousHp - current;
                if (dmg > 0f)
                    DamagePopup.Spawn(transform.position + Vector3.up * (_barWorldY * 0.85f), dmg);
            }
            _previousHp = current;

            RefreshFill(current, max);
        }

        private void RefreshFill(float current, float max)
        {
            if (_fillRect == null || _fillImage == null) return;

            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            // 用 anchorMax.x 代替 fillAmount，完全不依赖 Sprite
            _fillRect.anchorMax = new Vector2(ratio, 1f);
            _fillRect.offsetMax = Vector2.zero;

            _fillImage.color = ratio <= lowHpThreshold ? lowHpColor : healthyColor;
        }

        private void OnDestroy()
        {
            // Canvas 没有作为子物体，需要手动销毁
            if (_canvasGo != null)
                Destroy(_canvasGo);
        }
    }
}
