// ============================================================================
// ETD.Core - GameEvents.cs
// All game event structs. Using structs for zero-allocation publishing.
// NOTE: Enums (StatusEffectType, TileSpecialty, RelicGrade) live in ETD.Data
// to avoid circular assembly references. Events that reference those enums
// use int fields instead, so Core stays dependency-free.
// ============================================================================
using UnityEngine;

namespace ETD.Core
{
    public enum DamageNumberKind
    {
        Normal = 0,
        Critical = 1,
        Pure = 2,
        Burn = 3,
        Chain = 4,
        Laser = 5,
        Area = 6,
        Bonus = 7
    }

    // === GAME FLOW ===
    public struct GameStartedEvent { }
    public struct GameOverEvent { public int Score; public int WavesCompleted; }
    public struct DemoCompletedEvent { public int ReachedWave; }
    public struct GamePausedEvent { public bool IsPaused; }
    public struct RetryEvent {  }
    public struct WaveStartedEvent { public int WaveNumber; }
    public struct WaveCompletedEvent { public int WaveNumber; }
    public struct PrepPhaseStartedEvent { public float Duration; }
    public struct PrepPhaseSkipedEvent { public int Reward; }

    // === ECONOMY ===
    public struct GoldChangedEvent { public int Current; public int Delta; }
    public struct MetaCurrencyChangedEvent { public int Current; public int Delta; }
    public struct ScoreChangedEvent { public int Current; }

    // === PLAYER ===
    public struct LivesChangedEvent { public int Current; public int Max; }
    public struct XPGainedEvent { public float Amount; public float CurrentXP; public float RequiredXP; public int Level; }
    public struct LevelUpEvent { public int NewLevel; }
    public struct SpecCardChosenEvent { public int CardIndex; }
    public struct SpecCardChosenAfterEvent { }

    // === ENEMIES ===
    public struct EnemySpawnedEvent { public int EnemyId; public Vector3 Position; }
    public struct EnemyDamagedEvent
    {
        public int EnemyId;
        public int RuntimeId;
        public float Amount;
        public Vector3 WorldPosition;
        public bool IsCritical;
        public bool IsKillingBlow;
        public int DamageKind; // cast to DamageNumberKind
    }
    public struct EnemyKilledEvent { public int EnemyId; public int GoldReward; public float XPReward; public Vector3 Position; public int EnemyTier; }
    public struct EliteEnemyKilled { };
    public struct EnemyBurnedKilledEvent { public int EnemyId;}
    public struct EnemyReachedEndEvent { public int EnemyId; public int Damage; }
    public struct EnemySelectedEvent { public int EnemyId; }
    public struct EnemyDeselectedEvent { }
    public struct EnemyStatusAppliedEvent { public int EnemyId; public int StatusType; public float Duration; }
    public struct EnemyStatusExpiredEvent { public int EnemyId; public int StatusType; }
    public struct ChainLightningFiredEvent { public int HitCount; public Vector3 Origin; }
    public struct LaserHitEvent { public int TargetEnemyId; public float DeltaTime; }

    // Direct sink used by laser turrets to avoid EventBus overhead in heavy continuous-beam waves.
    // EventBus LaserHitEvent remains as a compatibility fallback.
    public interface ILaserHitTelemetrySink
    {
        void OnLaserHitBatch(float deltaTime, int targetEnemyId);
    }

    public struct PercentHPDamageEvent { public float DamageAmount; }

    public struct GameStateChangedEvent
    {
        public int NewState;  // cast to GameState enum
    }

    // === TURRETS ===
    public struct TurretPlacedEvent { public int TurretId; public Vector2Int GridPos; public int TurretType; }
    public struct TurretSoldEvent { public int TurretId; public int RefundAmount; }
    public struct TurretUpgradedEvent { public int TurretId; public int NewLevel; }
    public struct TurretEvolvedEvent { public int TurretId; public int EvolutionPath; }
    public struct TurretSelectedEvent { public int TurretId; }
    public struct TurretDeselectedEvent { }
    public struct ProjectileSpawnedEvent { }
    public struct ProjectileSpawnedBatchEvent { public int Count; }
    public struct BuffTurretsEvent { public int Count; }
    public struct ChainLightningHitEvent { public int Count; public int EnemyId; }

    // Batched once per frame by ChainLightningHitBatcher. The list is valid only
    // during EventBus.Publish; subscribers must consume it synchronously.
    public struct ChainLightningHitBatchEvent
    {
        public int Count;
        public System.Collections.Generic.List<int> EnemyIds;
    }
    public struct CriticalHitEvent {  }
    public struct SniperDamageEvent { public float Damage; }



    // === GRID / TILES ===
    public struct TileSpecialtyAppliedEvent { public Vector2Int GridPos; public int Specialty; }
    public struct PathRecalculatedEvent { public System.Collections.Generic.List<UnityEngine.Vector2Int> NewPath; }

    // === UI ===
    public struct LanguageChangedEvent
    {
        public string LanguageCode;
    }
    public struct ShowEvolveChoiceEvent { public int TurretId; }

    // v1.0: level-25 shared second evolution tier (converges from either Path A or
    // B chosen at the first evolution, so unlike ShowEvolveChoiceEvent there is no
    // path to pick — this is a single confirm, not an A/B choice). See [[etd-v1-full-release]].
    public struct ShowEvolveTier2ChoiceEvent { public int TurretId; }

    // v1.0 active spell system. Published by SpellManager.TryCast() on a successful
    // cast (UI/VFX/achievement hooks). See [[etd-v1-full-release]] Phase 2.
    public struct SpellCastEvent { public string SpellId; }

    public struct SettingToggleEvent { public bool IsActive; }


    public struct ChallengeCompletedEvent { public string ChallengeId; }
    public struct ShopItemPurchasedEvent { public string ItemId; }
    //public struct ScoreChangedEvent { public int Current; public int Delta; }

    // Published when any hub window opens (HubController.ToggleWindow)
    public struct HubWindowOpenedEvent
    {
        public HubWindowType WindowType;
    }

    public enum HubWindowType
    {
        Planning, Challenges, Shop, Turrets, Settings, Language
    }



    public enum UnlockType
    {
        Challenge = 0,
        Trait = 1,
        Turret = 2
    }

    public struct UnlockNotificationEvent
    {
        public int UnlockType;    // cast to UnlockType enum
        public string DisplayName;
        public string Description;
    }
}
