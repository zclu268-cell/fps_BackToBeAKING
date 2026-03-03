using System;
using UnityEngine;

namespace RoguePulse
{
    public class TeleporterObjective : InteractableBase
    {
        public enum TeleporterState
        {
            Idle = 0,
            Active = 1,
            Completed = 2
        }

        [Header("Charge")]
        [SerializeField] private float chargeSeconds = 90f;
        [SerializeField] private float outsideDecayMultiplier = 0.25f;
        [SerializeField] private float holdRadius = 2.8f;

        [Header("Visual")]
        [SerializeField] private Renderer teleporterRenderer;
        [SerializeField] private Color idleColor = new Color(0.30f, 0.90f, 1f);
        [SerializeField] private Color activeColor = new Color(1f, 0.80f, 0.20f);
        [SerializeField] private Color completedColor = new Color(0.35f, 1f, 0.35f);

        private float _timer;
        private bool _playerInside;
        private Transform _player;

        public TeleporterState State { get; private set; } = TeleporterState.Idle;
        public float Progress01 => Mathf.Clamp01(_timer / Mathf.Max(1f, chargeSeconds));

        public event Action<TeleporterState> OnStateChanged;
        public event Action<float> OnProgressChanged;
        public event Action OnCompleted;

        private void Start()
        {
            if (teleporterRenderer == null)
            {
                teleporterRenderer = GetComponentInChildren<Renderer>();
            }

            SphereCollider zone = GetComponent<SphereCollider>();
            if (zone != null)
            {
                holdRadius = Mathf.Max(0.5f, zone.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z));
            }

            UpdateVisual();
            OnProgressChanged?.Invoke(0f);
        }

        private void Update()
        {
            if (State != TeleporterState.Active)
            {
                return;
            }

            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning)
            {
                return;
            }

            UpdatePlayerInside();

            float delta = _playerInside ? Time.deltaTime : -(Time.deltaTime * outsideDecayMultiplier);
            _timer = Mathf.Clamp(_timer + delta, 0f, chargeSeconds);
            OnProgressChanged?.Invoke(Progress01);

            if (Progress01 >= 1f)
            {
                Complete();
            }
        }

        public override string Prompt
        {
            get
            {
                if (State == TeleporterState.Idle)
                {
                    return "Press E to activate teleporter";
                }

                if (State == TeleporterState.Active)
                {
                    return "Teleporter charging...";
                }

                return "Teleporter complete";
            }
        }

        public override bool CanInteract(GameObject interactor)
        {
            if (interactor == null || !interactor.CompareTag("Player"))
            {
                return false;
            }

            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning)
            {
                return false;
            }

            return State == TeleporterState.Idle;
        }

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            State = TeleporterState.Active;
            _timer = 0f;
            OnStateChanged?.Invoke(State);
            OnProgressChanged?.Invoke(0f);
            UpdateVisual();
        }

        public void GrantEliteKillBonus(float bonus01 = 0.02f)
        {
            if (State != TeleporterState.Active)
            {
                return;
            }

            _timer = Mathf.Clamp(_timer + chargeSeconds * Mathf.Max(0f, bonus01), 0f, chargeSeconds);
            OnProgressChanged?.Invoke(Progress01);
            if (Progress01 >= 1f)
            {
                Complete();
            }
        }

        public void ResetObjective(bool notifyState = true)
        {
            State = TeleporterState.Idle;
            _timer = 0f;
            _playerInside = false;
            if (notifyState)
            {
                OnStateChanged?.Invoke(State);
            }

            OnProgressChanged?.Invoke(0f);
            UpdateVisual();
        }

        private void Complete()
        {
            if (State == TeleporterState.Completed)
            {
                return;
            }

            State = TeleporterState.Completed;
            OnStateChanged?.Invoke(State);
            OnProgressChanged?.Invoke(1f);
            OnCompleted?.Invoke();
            UpdateVisual();
        }

        private void UpdatePlayerInside()
        {
            if (_player == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    _player = player.transform;
                }
            }

            if (_player == null)
            {
                _playerInside = false;
                return;
            }

            _playerInside = Vector3.Distance(_player.position, transform.position) <= holdRadius;
        }

        private void UpdateVisual()
        {
            if (teleporterRenderer == null)
            {
                return;
            }

            if (State == TeleporterState.Idle)
            {
                teleporterRenderer.material.color = idleColor;
            }
            else if (State == TeleporterState.Active)
            {
                teleporterRenderer.material.color = activeColor;
            }
            else
            {
                teleporterRenderer.material.color = completedColor;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other != null && other.CompareTag("Player"))
            {
                _playerInside = true;
                _player = other.transform;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other != null && other.CompareTag("Player"))
            {
                _playerInside = false;
            }
        }
    }
}
