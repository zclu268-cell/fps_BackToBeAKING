using UnityEngine;

namespace RoguePulse
{
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float lifeSeconds = 4f;
        [SerializeField] private bool destroyOnHit = true;
        [SerializeField] private bool destroyOnNonDamageableHit = false;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField, Range(1, 16)] private int maxSweepHits = 8;
        [Header("Size")]
        [SerializeField] private bool autoOptimizeSize = true;
        [SerializeField, Min(0.02f)] private float maxVisualScale = 0.14f;
        [SerializeField, Min(0.005f)] private float minHitWorldRadius = 0.035f;
        [Header("VFX")]
        [SerializeField] private GameObject impactEffectPrefab;
        [SerializeField, Min(0.1f)] private float impactEffectLifetime = 1.2f;
        [SerializeField] private bool useFallbackImpactVfx = true;
        [SerializeField] private Color fallbackImpactColor = new Color(1f, 0.84f, 0.28f, 1f);
        [SerializeField, Min(0.1f)] private float fallbackImpactScale = 1f;
        [SerializeField] private bool autoAddTrail = true;
        [SerializeField] private Color trailColor = new Color(1f, 0.88f, 0.32f, 1f);
        [SerializeField, Min(0.01f)] private float trailTime = 0.14f;
        [SerializeField, Min(0.002f)] private float trailStartWidth = 0.06f;
        [SerializeField, Min(0.001f)] private float trailEndWidth = 0.008f;

        private Vector3 _direction;
        private float _speed;
        private float _damage;
        private Damageable _owner;
        private float _dieAt;
        private bool _initialized;
        private bool _impacted;
        private SphereCollider _trigger;
        private Rigidbody _rb;
        private RaycastHit[] _sweepHits;
        private TrailRenderer _trail;

        private static Material _sharedTrailMaterial;
        private static Material _sharedImpactMaterial;

        public void Initialize(Vector3 direction, float speed, float damage, Damageable owner)
        {
            _direction = direction.normalized;
            _speed = Mathf.Max(0f, speed);
            _damage = Mathf.Max(0f, damage);
            _owner = owner;
            _dieAt = Time.time + lifeSeconds;
            _initialized = true;
            _impacted = false;

            if (_trail != null)
            {
                _trail.Clear();
            }
        }

        private void Awake()
        {
            _trigger = GetComponent<SphereCollider>();
            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody>();
            }

            _trigger.isTrigger = true;
            _rb.useGravity = false;
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            _sweepHits = new RaycastHit[Mathf.Clamp(maxSweepHits, 1, 16)];
            OptimizeVisualSize();
            EnsureTrail();
        }

        private void Update()
        {
            if (!_initialized || _impacted)
            {
                return;
            }

            if (Time.time >= _dieAt)
            {
                Destroy(gameObject);
            }
        }

        private void FixedUpdate()
        {
            if (!_initialized || _impacted || _speed <= 0f)
            {
                return;
            }

            Vector3 current = _rb.position;
            float stepDistance = _speed * Time.fixedDeltaTime;
            Vector3 next = current + _direction * stepDistance;

            if (TrySweepHit(current, stepDistance, out RaycastHit hit))
            {
                ProcessHit(hit.collider, hit.point, hit.normal);
                if (_impacted)
                {
                    return;
                }
            }

            _rb.MovePosition(next);
        }

        private bool TrySweepHit(Vector3 origin, float distance, out RaycastHit hit)
        {
            hit = default;
            if (distance <= 0.0001f)
            {
                return false;
            }

            float worldRadius = GetWorldRadius();
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                worldRadius,
                _direction,
                _sweepHits,
                distance,
                hitMask,
                QueryTriggerInteraction.Collide);

            if (hitCount <= 0)
            {
                return false;
            }

            float nearest = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                Collider c = _sweepHits[i].collider;
                if (!CanCollideWith(c))
                {
                    continue;
                }

                float d = _sweepHits[i].distance;
                if (d < nearest)
                {
                    nearest = d;
                    hit = _sweepHits[i];
                }
            }

            return hit.collider != null;
        }

        private float GetWorldRadius()
        {
            if (_trigger == null)
            {
                return Mathf.Max(0.05f, minHitWorldRadius);
            }

            Vector3 s = transform.lossyScale;
            float scale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            return Mathf.Max(minHitWorldRadius, Mathf.Max(0.01f, _trigger.radius * scale));
        }

        private bool CanCollideWith(Collider other)
        {
            if (other == null)
            {
                return false;
            }

            Transform otherTf = other.transform;
            if (otherTf == transform || otherTf.IsChildOf(transform))
            {
                return false;
            }

            if (_owner != null)
            {
                Transform ownerTf = _owner.transform;
                if (otherTf == ownerTf || otherTf.IsChildOf(ownerTf))
                {
                    return false;
                }
            }

            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_initialized || _impacted || !CanCollideWith(other))
            {
                return;
            }

            Vector3 point = GetSafeClosestPoint(other, transform.position);
            Vector3 normal = (transform.position - point).normalized;
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = -_direction;
            }

            ProcessHit(other, point, normal);
        }

        private void ProcessHit(Collider other, Vector3 hitPoint, Vector3 hitNormal)
        {
            Damageable target = other.GetComponentInParent<Damageable>();
            if (target != null && target != _owner)
            {
                target.TakeDamage(_damage);
                SpawnImpactVfx(hitPoint, hitNormal, onDamageable: true);
                if (destroyOnHit)
                {
                    Impact();
                }
                return;
            }

            if (destroyOnNonDamageableHit)
            {
                SpawnImpactVfx(hitPoint, hitNormal, onDamageable: false);
                Impact();
            }
        }

        private static Vector3 GetSafeClosestPoint(Collider collider, Vector3 referencePoint)
        {
            if (collider == null)
            {
                return referencePoint;
            }

            if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider)
            {
                return collider.ClosestPoint(referencePoint);
            }

            if (collider is MeshCollider meshCollider && meshCollider.convex)
            {
                return collider.ClosestPoint(referencePoint);
            }

            Bounds bounds = collider.bounds;
            return bounds.size.sqrMagnitude > 0f
                ? bounds.ClosestPoint(referencePoint)
                : collider.transform.position;
        }

        private void Impact()
        {
            if (_impacted)
            {
                return;
            }

            _impacted = true;
            Destroy(gameObject);
        }

        private void OptimizeVisualSize()
        {
            if (!autoOptimizeSize)
            {
                return;
            }

            Vector3 current = transform.localScale;
            float maxAxis = Mathf.Max(Mathf.Abs(current.x), Mathf.Abs(current.y), Mathf.Abs(current.z));
            if (maxAxis <= 0.0001f)
            {
                transform.localScale = Vector3.one * maxVisualScale;
                return;
            }

            if (maxAxis <= maxVisualScale)
            {
                return;
            }

            float mul = maxVisualScale / maxAxis;
            transform.localScale = current * mul;
        }

        private void EnsureTrail()
        {
            if (!autoAddTrail)
            {
                return;
            }

            _trail = GetComponent<TrailRenderer>();
            if (_trail == null)
            {
                _trail = gameObject.AddComponent<TrailRenderer>();
            }

            _trail.time = trailTime;
            _trail.startWidth = trailStartWidth;
            _trail.endWidth = trailEndWidth;
            _trail.minVertexDistance = 0.02f;
            _trail.numCapVertices = 2;
            _trail.emitting = true;
            _trail.autodestruct = false;
            _trail.alignment = LineAlignment.View;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(trailColor, 0f),
                    new GradientColorKey(trailColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.92f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            _trail.colorGradient = gradient;

            Material mat = GetSharedTrailMaterial();
            if (mat != null)
            {
                _trail.sharedMaterial = mat;
            }
        }

        private void SpawnImpactVfx(Vector3 hitPoint, Vector3 hitNormal, bool onDamageable)
        {
            Quaternion rotation = hitNormal.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(hitNormal.normalized, Vector3.up)
                : Quaternion.identity;

            if (impactEffectPrefab != null)
            {
                GameObject fx = Instantiate(impactEffectPrefab, hitPoint, rotation);
                if (impactEffectLifetime > 0f)
                {
                    Destroy(fx, impactEffectLifetime);
                }
                return;
            }

            if (!useFallbackImpactVfx)
            {
                return;
            }

            GameObject go = new GameObject("ProjectileImpactVfx");
            go.transform.position = hitPoint + hitNormal.normalized * 0.02f;
            go.transform.rotation = rotation;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            // Ensure the particle is not playing before runtime parameter assignment.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.07f, 0.18f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f * fallbackImpactScale, 4.8f * fallbackImpactScale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f * fallbackImpactScale, 0.11f * fallbackImpactScale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.stopAction = ParticleSystemStopAction.Destroy;

            Color burstColor = onDamageable
                ? fallbackImpactColor
                : Color.Lerp(fallbackImpactColor, Color.white, 0.35f);
            main.startColor = burstColor;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.Burst burst = new ParticleSystem.Burst(0f, (short)14, (short)22);
            emission.SetBursts(new[] { burst });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = 0.01f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fadeGradient = new Gradient();
            fadeGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(burstColor, 0f),
                    new GradientColorKey(Color.Lerp(burstColor, Color.white, 0.25f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = fadeGradient;

            ParticleSystemRenderer psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            Material impactMat = GetSharedImpactMaterial();
            if (impactMat != null)
            {
                psRenderer.sharedMaterial = impactMat;
            }

            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();

            float destroyAfter = Mathf.Max(impactEffectLifetime, 0.5f);
            Destroy(go, destroyAfter);
        }

        private static Material GetSharedTrailMaterial()
        {
            if (_sharedTrailMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                if (shader != null)
                {
                    _sharedTrailMaterial = new Material(shader)
                    {
                        name = "ProjectileTrailRuntimeMat"
                    };
                }
            }

            return _sharedTrailMaterial;
        }

        private static Material GetSharedImpactMaterial()
        {
            if (_sharedImpactMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Particles/Standard Unlit");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }

                if (shader != null)
                {
                    _sharedImpactMaterial = new Material(shader)
                    {
                        name = "ProjectileImpactRuntimeMat"
                    };
                }
            }

            return _sharedImpactMaterial;
        }
    }
}
