// ============================================================================
// ETD.UI - TurretLevelBadgeManager.cs
// Optional global helper: add this once to the HUD scene and it will attach
// TurretLevelBadge to every placed/restored turret at runtime.
// ============================================================================
using UnityEngine;
using ETD.Core;
using ETD.Turrets;

namespace ETD.UI
{
    [DisallowMultipleComponent]
    public sealed class TurretLevelBadgeManager : MonoBehaviour
    {
        [Header("Feature Toggle")]
        [SerializeField] private bool _enableLevelBadges = true;

        [Header("References")]
        [SerializeField] private Camera _worldCamera;
        [SerializeField] private bool _attachToExistingOnStart = true;

        private TurretManager _turretManager;

        private void Start()
        {
            if (_worldCamera == null)
                _worldCamera = Camera.main;
            ServiceLocator.TryGet(out _turretManager);

            if (_enableLevelBadges && _attachToExistingOnStart)
                AttachToExistingTurrets();
            else if (!_enableLevelBadges)
                SetExistingBadgesEnabled(false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
                return;

            if (_enableLevelBadges)
                AttachToExistingTurrets();
            else
                SetExistingBadgesEnabled(false);
        }
#endif

        private void OnEnable()
        {
            EventBus.Subscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Subscribe<TurretUpgradedEvent>(OnTurretUpdated);
            EventBus.Subscribe<TurretEvolvedEvent>(OnTurretEvolved);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Unsubscribe<TurretUpgradedEvent>(OnTurretUpdated);
            EventBus.Unsubscribe<TurretEvolvedEvent>(OnTurretEvolved);
        }

        private void OnTurretPlaced(TurretPlacedEvent evt) => EnsureBadge(evt.TurretId);
        private void OnTurretUpdated(TurretUpgradedEvent evt) => EnsureBadge(evt.TurretId);
        private void OnTurretEvolved(TurretEvolvedEvent evt) => EnsureBadge(evt.TurretId);

        private void EnsureBadge(int turretId)
        {
            if (!_enableLevelBadges)
                return;

            if (_turretManager == null)
                ServiceLocator.TryGet(out _turretManager);

            TurretController turret = _turretManager != null ? _turretManager.GetTurret(turretId) : null;
            if (turret == null)
                return;

            TurretLevelBadge badge = turret.GetComponent<TurretLevelBadge>();
            if (badge == null)
                badge = turret.gameObject.AddComponent<TurretLevelBadge>();

            badge.Bind(turret, _worldCamera);
            badge.SetManagerEnabled(true);
        }

        private void AttachToExistingTurrets()
        {
            if (!_enableLevelBadges)
            {
                SetExistingBadgesEnabled(false);
                return;
            }

#if UNITY_2023_1_OR_NEWER
            TurretController[] turrets = Object.FindObjectsByType<TurretController>(FindObjectsSortMode.None);
#else
            TurretController[] turrets = Object.FindObjectsOfType<TurretController>();
#endif
            for (int i = 0; i < turrets.Length; i++)
            {
                if (turrets[i] == null)
                    continue;

                TurretLevelBadge badge = turrets[i].GetComponent<TurretLevelBadge>();
                if (badge == null)
                    badge = turrets[i].gameObject.AddComponent<TurretLevelBadge>();

                badge.Bind(turrets[i], _worldCamera);
                badge.SetManagerEnabled(true);
            }
        }

        private void SetExistingBadgesEnabled(bool enabled)
        {
#if UNITY_2023_1_OR_NEWER
            TurretLevelBadge[] badges = Object.FindObjectsByType<TurretLevelBadge>(FindObjectsSortMode.None);
#else
            TurretLevelBadge[] badges = Object.FindObjectsOfType<TurretLevelBadge>();
#endif
            for (int i = 0; i < badges.Length; i++)
            {
                if (badges[i] == null)
                    continue;

                badges[i].SetManagerEnabled(enabled);
            }
        }
    }
}
