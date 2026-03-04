using UnityEngine;

namespace RoguePulse
{
    /// <summary>
    /// Enemy 鍔ㄧ敾鎺у埗鍣ㄢ€斺€旂▼搴忓寲鏁堟灉 + Animator 椹卞姩銆?    ///
    /// 鏀寔鐨勬晥鏋滐細
    ///   鈥?寰呮満鎽囨憜   (Idle Sway)          鈥?闈欐鏃惰韩浣撹交寰乏鍙虫憞鎽?    ///   鈥?琛岃蛋寮硅烦   (Walk Bob)            鈥?绉诲姩鏃朵笂涓嬪脊璺?    ///   鈥?鍙楀嚮闂櫧   (Hit Flash)           鈥?鍙楀埌浼ゅ鏃跺叏韬櫧鑹查棯鍏?    ///   鈥?姝讳骸鍊掑湴   (Death Fall)          鈥?姝讳骸鏃跺悜鍓嶆棆杞€掍笅
    ///   鈥?杩戞垬鏀诲嚮   (Melee Attack)        鈥?瑙﹀彂 Attack trigger 鈫?鎾斁涓嬪妶鍔ㄧ敾
    ///   鈥?鍙椾激濂旇窇   (Injured Run)         鈥?HP 浣庝簬闃堝€兼椂鍒囨崲涓哄彈浼よ窇姝ュ姩鐢?    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class EnemyAnimationController : MonoBehaviour
    {
        [Header("Model Root (auto-pick first child with Renderer when empty)")]
        [SerializeField] private Transform modelRoot;

        [Header("寰呮満鎽囨憜")]
        [SerializeField] private float idleSwaySpeed = 1.4f;
        [SerializeField] private float idleSwayAngle = 2.5f;

        [Header("琛岃蛋寮硅烦")]
        [SerializeField] private float bobFrequency = 8f;
        [SerializeField] private float bobAmplitude = 0.06f;

        [Header("鍙楀嚮闂厜")]
        [SerializeField] private float hitFlashDuration = 0.1f;

        [Header("姝讳骸鍊掑湴")]
        [SerializeField] private float deathFallSpeed = 280f;
        [SerializeField] private float deathFallMaxAngle = 90f;

        [Header("鍙椾激璺戞")]
        [Tooltip("HP 鐧惧垎姣斾綆浜庢鍊兼椂鍒囨崲涓哄彈浼よ窇姝ュ姩鐢?(0~1)")]
        [SerializeField] [Range(0f, 1f)] private float injuredThreshold = 0.5f;

        [Header("Ground Anti-Clipping")]
        [SerializeField] private bool keepFeetAboveGround = false;
        [SerializeField] private float feetGroundClearance = 0.03f;
        [SerializeField] private float maxGroundCorrectionPerFrame = 0.2f;
        // 鈹€鈹€ Animator 鍙傛暟鍚嶅父閲?鈹€鈹€
        private static readonly int ParamSpeed     = Animator.StringToHash("Speed");
        private static readonly int ParamAttack    = Animator.StringToHash("Attack");
        private static readonly int ParamAttackAlt = Animator.StringToHash("Attack1");
        private static readonly int ParamHit       = Animator.StringToHash("Hit");
        private static readonly int ParamDamage    = Animator.StringToHash("Damage");
        private static readonly int ParamIsInjured = Animator.StringToHash("IsInjured");
        private static readonly int ParamInjured   = Animator.StringToHash("Injured");
        // 鈹€鈹€ 鍐呴儴鐘舵€?鈹€鈹€
        private Damageable   _damageable;
        private Animator     _animator;
        private bool         _hasAnimator;
        private float        _swayTime;
        private float        _bobTime;
        private Vector3      _lastPos;
        private Vector3      _baseLocalPos;

        // 鏉愯川闂厜
        private Renderer[] _renderers;
        private Material[][] _originalMats;
        private Material[]   _flashMats;
        private Material[][] _flashMatArrays;  // 棰勫垎閰嶏紝閬垮厤姣忔鍙楀嚮 new 鏁扮粍s;
        private float        _flashTimer;
        private bool         _flashing;

        // 姝讳骸
        private bool  _dead;
        private float _deathAngle;
        private bool _groundAligned;
        private Transform[] _feetBones;
        private float _canonicalModelLocalY = float.NaN;
        private bool _hasSpeedParam;
        private bool _hasAttackTrigger;
        private bool _hasAttackAltTrigger;
        private bool _hasHitTrigger;
        private bool _hasDamageTrigger;
        private bool _hasIsInjuredParam;
        private bool _hasInjuredParam;
        private float _lastObservedHp = float.MaxValue;
        // 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€ 鐢熷懡鍛ㄦ湡 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

        private void Awake()
        {
            _damageable = GetComponent<Damageable>();
            if (modelRoot == null)
                modelRoot = FindModelRoot(transform);
        }

        private void Start()
        {
            _lastPos = transform.position;
            if (_damageable != null)
            {
                _lastObservedHp = _damageable.CurrentHp;
            }
            if (modelRoot != null)
            {
                _baseLocalPos = modelRoot.localPosition;
                _animator     = modelRoot.GetComponentInChildren<Animator>(true);
                if (_animator != null)
                {
                    _animator.applyRootMotion = false;
                }
            }
            _hasAnimator = _animator != null && _animator.runtimeAnimatorController != null;
            CacheAnimatorParameters();
            CacheFeetBones();
            CacheMaterials();
        }

        private void OnEnable()
        {
            if (_damageable != null)
            {
                _damageable.OnHealthChanged += HandleHealthChanged;
                _damageable.OnDeath         += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (_damageable != null)
            {
                _damageable.OnHealthChanged -= HandleHealthChanged;
                _damageable.OnDeath         -= HandleDeath;
            }
        }

        private void LateUpdate()
        {
            if (modelRoot == null) return;

            // 鈹€鈹€ 姝讳骸鍊掑湴 鈹€鈹€
            if (_dead)
            {
                _deathAngle = Mathf.Min(_deathAngle + deathFallSpeed * Time.deltaTime, deathFallMaxAngle);
                modelRoot.localRotation = Quaternion.Euler(_deathAngle, 0f, 0f);
                ResolveGroundClipping();
                return;
            }

            // 鈹€鈹€ 閫熷害 鈹€鈹€
            float speed = ((transform.position - _lastPos) / Mathf.Max(Time.deltaTime, 0.0001f)).magnitude;
            _lastPos    = transform.position;
            bool moving = speed > 0.15f;

            // 鈹€鈹€ 椹卞姩 Animator 鍙傛暟 鈹€鈹€
            if (_hasAnimator && _hasSpeedParam)
            {
                _animator.SetFloat(ParamSpeed, speed);
            }

            // 鈹€鈹€ 琛岃蛋寮硅烦 vs 寰呮満鎽囨憜锛堜粎鍦ㄦ棤 Animator 鎴?Animator 鏃?RuntimeController 鏃朵娇鐢ㄧ▼搴忓寲鍔ㄧ敾锛夆攢鈹€
            if (!_hasAnimator)
            {
                if (moving)
                {
                    _bobTime  += Time.deltaTime * bobFrequency;
                    _swayTime  = 0f;
                    float bob  = Mathf.Sin(_bobTime) * bobAmplitude;
                    modelRoot.localPosition = Vector3.Lerp(
                        modelRoot.localPosition,
                        _baseLocalPos + Vector3.up * bob,
                        Time.deltaTime * 20f);
                    modelRoot.localRotation = Quaternion.Slerp(
                        modelRoot.localRotation, Quaternion.identity, Time.deltaTime * 10f);
                }
                else
                {
                    _bobTime   = 0f;
                    _swayTime += Time.deltaTime * idleSwaySpeed;
                    float sway = Mathf.Sin(_swayTime) * idleSwayAngle;
                    modelRoot.localPosition = Vector3.Lerp(
                        modelRoot.localPosition, _baseLocalPos, Time.deltaTime * 8f);
                    modelRoot.localRotation = Quaternion.Slerp(
                        modelRoot.localRotation,
                        Quaternion.Euler(0f, 0f, sway),
                        Time.deltaTime * 6f);
                }
            }

            // 鈹€鈹€ 闂厜鎭㈠ 鈹€鈹€
            if (_flashing)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f)
                {
                    RestoreMaterials();
                    _flashing = false;
                }
            }

            ResolveGroundClipping();
        }

        // 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€ 鍏紑 API 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

        /// <summary>
        /// 鐢?EnemyController 鍦ㄦ瘡娆℃敾鍑绘椂璋冪敤锛岃Е鍙戣繎鎴樻敾鍑诲姩鐢汇€?        /// </summary>
        public void TriggerAttack()
        {
            if (_hasAnimator)
            {
                if (_hasAttackTrigger)
                {
                    _animator.SetTrigger(ParamAttack);
                }

                if (_hasAttackAltTrigger)
                {
                    _animator.SetTrigger(ParamAttackAlt);
                }
            }
        }

        public void SetModelRoot(Transform newRoot)
        {
            modelRoot = newRoot;
            _baseLocalPos = modelRoot != null ? modelRoot.localPosition : Vector3.zero;
            _canonicalModelLocalY = float.NaN; // re-capture on next LateUpdate
            _animator = modelRoot != null ? modelRoot.GetComponentInChildren<Animator>(true) : null;
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }
            _hasAnimator = _animator != null && _animator.runtimeAnimatorController != null;
            CacheAnimatorParameters();
            CacheFeetBones();
            CacheMaterials();
        }

        public void ConfigureGroundLock(bool enabled, float clearance)
        {
            keepFeetAboveGround = enabled;
            feetGroundClearance = Mathf.Max(0f, clearance);
            maxGroundCorrectionPerFrame = Mathf.Clamp(maxGroundCorrectionPerFrame, 0.005f, 0.08f);
            _groundAligned = false;
            _canonicalModelLocalY = float.NaN; // re-capture on next LateUpdate after alignment settles
        }

        // 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€ 浜嬩欢澶勭悊 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

        private void HandleHealthChanged(Damageable d, float current, float max)
        {
            bool tookDamage = current < _lastObservedHp - 0.0001f;
            _lastObservedHp = current;

            // 鍙楀埌浼ゅ锛圚P 鍑忓皯锛夋椂瑙﹀彂闂厜 + 受击动画
            if (tookDamage && !_dead)
            {
                TriggerHitFlash();
                TriggerHitReactionAnimation();
            }

            // 鏇存柊鍙椾激鐘舵€
            if (_hasAnimator && max > 0f)
            {
                bool injured = (current / max) < injuredThreshold;
                if (_hasIsInjuredParam)
                {
                    _animator.SetBool(ParamIsInjured, injured);
                }

                if (_hasInjuredParam)
                {
                    _animator.SetBool(ParamInjured, injured);
                }
            }
        }

        private void HandleDeath(Damageable d)
        {
            _dead = true;
            if (_hasAnimator)
            {
                if (_hasIsInjuredParam)
                {
                    _animator.SetBool(ParamIsInjured, false);
                }

                if (_hasInjuredParam)
                {
                    _animator.SetBool(ParamInjured, false);
                }

                if (_hasAttackTrigger)
                {
                    _animator.ResetTrigger(ParamAttack);
                }

                if (_hasAttackAltTrigger)
                {
                    _animator.ResetTrigger(ParamAttackAlt);
                }

                if (_hasHitTrigger)
                {
                    _animator.ResetTrigger(ParamHit);
                }

                if (_hasDamageTrigger)
                {
                    _animator.ResetTrigger(ParamDamage);
                }
            }
            // 姝讳骸鏃舵仮澶嶆潗璐紙闃叉鐧借壊鐘舵€佹浜★級
            if (_flashing) RestoreMaterials();
        }

        // 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€ 鍙楀嚮闂櫧 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

private void TriggerHitFlash()
        {
            if (_renderers == null || _flashMatArrays == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].sharedMaterials = _flashMatArrays[i];  // 澶嶇敤棰勫垎閰嶆暟缁勶紝闆?GC
            }

            _flashTimer = hitFlashDuration;
            _flashing   = true;
        }

        private void RestoreMaterials()
        {
            if (_renderers == null || _originalMats == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].sharedMaterials = _originalMats[i];
            }
        }

        private void TriggerHitReactionAnimation()
        {
            if (!_hasAnimator)
            {
                return;
            }

            if (_hasHitTrigger)
            {
                _animator.SetTrigger(ParamHit);
            }
            else if (_hasDamageTrigger)
            {
                _animator.SetTrigger(ParamDamage);
            }
        }

private void CacheMaterials()
        {
            _renderers      = GetComponentsInChildren<Renderer>(true);
            int n           = _renderers.Length;
            _originalMats   = new Material[n][];
            _flashMats      = new Material[n];
            _flashMatArrays = new Material[n][];

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");

            for (int i = 0; i < n; i++)
            {
                Material[] src = _renderers[i].sharedMaterials;
                _originalMats[i] = new Material[src.Length];
                System.Array.Copy(src, _originalMats[i], src.Length);

                if (shader != null)
                {
                    Material flash = new Material(shader);
                    flash.color    = Color.white;
                    _flashMats[i]  = flash;
                }
                // 棰勫垎閰嶅崟鍏冪礌鏁扮粍锛屽悗缁?TriggerHitFlash 鐩存帴澶嶇敤
                _flashMatArrays[i] = new Material[] { _flashMats[i] };
            }
        }

        private void CacheAnimatorParameters()
        {
            _hasSpeedParam = false;
            _hasAttackTrigger = false;
            _hasAttackAltTrigger = false;
            _hasHitTrigger = false;
            _hasDamageTrigger = false;
            _hasIsInjuredParam = false;
            _hasInjuredParam = false;

            if (!_hasAnimator || _animator == null)
            {
                return;
            }

            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Float && parameter.nameHash == ParamSpeed)
                {
                    _hasSpeedParam = true;
                }
                else if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.nameHash == ParamAttack)
                {
                    _hasAttackTrigger = true;
                }
                else if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.nameHash == ParamAttackAlt)
                {
                    _hasAttackAltTrigger = true;
                }
                else if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.nameHash == ParamHit)
                {
                    _hasHitTrigger = true;
                }
                else if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.nameHash == ParamDamage)
                {
                    _hasDamageTrigger = true;
                }
                else if (parameter.type == AnimatorControllerParameterType.Bool && parameter.nameHash == ParamIsInjured)
                {
                    _hasIsInjuredParam = true;
                }
                else if (parameter.type == AnimatorControllerParameterType.Bool && parameter.nameHash == ParamInjured)
                {
                    _hasInjuredParam = true;
                }
            }
        }

        private void ResolveGroundClipping()
        {
            if (!keepFeetAboveGround || modelRoot == null)
            {
                return;
            }

            float currentLocalY = modelRoot.localPosition.y;

            // Capture the canonical (spawn-aligned) local Y on first call so we have a stable
            // reference that isn't contaminated by animation-driven bone positions.
            if (float.IsNaN(_canonicalModelLocalY))
            {
                _canonicalModelLocalY = currentLocalY;
                if (!_groundAligned)
                {
                    _groundAligned = true;
                    _baseLocalPos = modelRoot.localPosition;
                }
                return;
            }

            float delta = _canonicalModelLocalY - currentLocalY;
            float step = Mathf.Max(0.005f, maxGroundCorrectionPerFrame);

            if (delta > 0.0001f)
            {
                // Model has sunk below the canonical aligned position — lift back up.
                Vector3 lp = modelRoot.localPosition;
                lp.y += Mathf.Min(delta, step);
                modelRoot.localPosition = lp;
            }
            else if (delta < -0.0001f)
            {
                // Model has risen above canonical (e.g. stale drift) — bring back down.
                Vector3 lp = modelRoot.localPosition;
                lp.y -= Mathf.Min(-delta, step);
                modelRoot.localPosition = lp;
            }

            if (!_groundAligned)
            {
                _groundAligned = true;
                _baseLocalPos = modelRoot.localPosition;
            }
        }

        private float GetBodyFeetWorldY()
        {
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                return transform.position.y + cc.center.y - cc.height * 0.5f;
            }

            CapsuleCollider cap = GetComponent<CapsuleCollider>();
            if (cap != null)
            {
                return transform.position.y + cap.center.y - cap.height * 0.5f;
            }

            return transform.position.y;
        }

        private void CacheFeetBones()
        {
            _feetBones = null;
            if (_animator == null || !_animator.isHuman)
            {
                return;
            }

            var bones = new System.Collections.Generic.List<Transform>(4);
            TryAddBone(bones, HumanBodyBones.LeftFoot);
            TryAddBone(bones, HumanBodyBones.RightFoot);
            TryAddBone(bones, HumanBodyBones.LeftToes);
            TryAddBone(bones, HumanBodyBones.RightToes);
            if (bones.Count > 0)
            {
                _feetBones = bones.ToArray();
            }
        }

        private void TryAddBone(System.Collections.Generic.List<Transform> list, HumanBodyBones bone)
        {
            Transform t = _animator.GetBoneTransform(bone);
            if (t != null)
            {
                list.Add(t);
            }
        }

        // Prefer bone-based foot Y (stable across animation frames) over renderer bounds (noisy).
        private bool TryGetFeetMinY(out float minY)
        {
            if (_feetBones != null && _feetBones.Length > 0)
            {
                minY = float.MaxValue;
                for (int i = 0; i < _feetBones.Length; i++)
                {
                    if (_feetBones[i] != null)
                    {
                        minY = Mathf.Min(minY, _feetBones[i].position.y);
                    }
                }

                return minY < float.MaxValue;
            }

            // Non-humanoid fallback: use renderer bounds with a stability bias.
            // Apply only when the model appears to have sunk clearly below the floor
            // to avoid the oscillation issue that prompted the original disable.
            if (!TryGetRenderersMinY(out float rendererMinY))
            {
                minY = 0f;
                return false;
            }

            float desiredMinY = GetBodyFeetWorldY() + feetGroundClearance;
            // Only report the value as "below floor" if clearly clipping (> 2x clearance threshold).
            if (rendererMinY < desiredMinY - Mathf.Max(0.02f, feetGroundClearance * 2f))
            {
                minY = rendererMinY;
                return true;
            }

            minY = 0f;
            return false;
        }

        private bool TryGetRenderersMinY(out float minY)
        {
            minY = 0f;
            // Use only renderers under modelRoot to avoid non-mesh renderers (particles,
            // projectors, etc.) on the root skewing the bounds calculation.
            Renderer[] renderers = modelRoot != null
                ? modelRoot.GetComponentsInChildren<Renderer>(true)
                : null;

            if (renderers == null || renderers.Length == 0)
            {
                return false;
            }

            bool found = false;
            float y = float.MaxValue;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }

                float candidate = renderer.bounds.min.y;
                if (!found || candidate < y)
                {
                    found = true;
                    y = candidate;
                }
            }

            if (!found)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (!(renderer is MeshRenderer))
                    {
                        continue;
                    }

                    float candidate = renderer.bounds.min.y;
                    if (!found || candidate < y)
                    {
                        found = true;
                        y = candidate;
                    }
                }
            }

            if (!found)
            {
                return false;
            }

            minY = y;
            return true;
        }

        // 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€ 宸ュ叿 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

        private static Transform FindModelRoot(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.GetComponentInChildren<Renderer>() != null)
                    return child;
            }
            return parent.childCount > 0 ? parent.GetChild(0) : null;
        }
    }
}
