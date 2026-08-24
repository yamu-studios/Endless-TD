// ============================================================================
// ETD.Core - GameConstants.cs  [UPDATED - matches ETD_FULL_SHEET]
// ============================================================================

namespace ETD.Core
{
    public static class GameConstants
    {
        // Grid (dimensions are dynamic — read from scene tiles)
        public const float CELL_SIZE = 1f;
        public const float SPAWN_X = -9f; 
        public const float SPAWN_Y = -4f; 
        public const float EXIT_X = 23f; 
        public const float EXIT_Y = -4f;

        // Player
        public const int STARTING_LIVES = 20;
        public const int STARTING_GOLD = 60;

        // Economy scaling — kill gold grows with wave so income stays in the same
        // growth class as enemy HP. goldPerKill = base * (1 + wave * GOLD_KILL_WAVE_SCALE),
        // multiplied by tier so elite/boss kills mirror their HP multipliers.
        // 0.07 (down from the initial 0.08 design) trims mid/late income ~10-12%.
        public const float GOLD_KILL_WAVE_SCALE = 0.07f;
        public const float ELITE_GOLD_MULTIPLIER = 5f;
        public const float BOSS_GOLD_MULTIPLIER = 25f;

        // Kill XP grows gently with wave so level-up pacing does not stall late
        // (XP requirements grow per level while base kill XP is flat).
        public const float XP_KILL_WAVE_SCALE = 0.05f;
        public const int MAX_TRAIT_SLOTS_DEFAULT = 2;
        public const int MAX_TRAIT_SLOTS_LIMIT = 5;

        // XP & Leveling
        public const float BASE_XP_REQUIRED = 50f;
        public const float XP_SCALING_FACTOR = 1.15f;
        public const int SPEC_CARDS_PER_LEVELUP = 3;

        // Waves
        public const float BASE_PREP_TIME = 15f;
        public const int ELITE_WAVE_INTERVAL = 5;
        public const int BOSS_WAVE_INTERVAL = 25;
        public const int INCREASED_ELITE_FREQUENCY_WAVE = 75;

        // Turrets
        public const int TURRET_EVOLVE_LEVEL = 15; // Changed from 20 to 15 per sheet
        public const float TURRET_SELL_REFUND_PERCENT = 0.6f;
        public const int DEFAULT_RELIC_SLOTS = 1;
        public const int MAX_RELIC_SLOTS = 2;

        // Enemies
        public const float META_CURRENCY_DROP_CHANCE = 0.02f;
        public const float ELITE_RELIC_DROP_CHANCE = 0.25f;
        public const float BOSS_RELIC_DROP_CHANCE = 1.0f;

        // Camera
        public const float CAMERA_PAN_SPEED = 10f;
        public const float CAMERA_ZOOM_SPEED = 5f;
        public const float CAMERA_MIN_ZOOM = 3f;
        public const float CAMERA_MAX_ZOOM = 15f;

        // Grade Offer Weights (from Grade_Table sheet)
        public const float COMMON_WEIGHT = 55f;
        public const float UNCOMMON_WEIGHT = 25f;
        public const float RARE_WEIGHT = 12f;
        public const float UNIQUE_WEIGHT = 6f;
        public const float LEGENDARY_WEIGHT = 2f;

        // Grade Power Budget (relative multiplier)
        public const float COMMON_POWER = 1.0f;
        public const float UNCOMMON_POWER = 1.35f;
        public const float RARE_POWER = 1.75f;
        public const float UNIQUE_POWER = 2.25f;
        public const float LEGENDARY_POWER = 3.2f;

        // Dynamic Tiles
        public const float TILE_SPECIALTY_CHANCE = 0.08f;//0.08
        public const float SPECIALTY_TILE_RATIO = 0.12f; // ~12% of valid tiles get a special

        // Enemy Scaling
        public const float ENEMY_HP_SCALE_PER_WAVE = 1.05f;
        public const float ENEMY_SPEED_SCALE_PER_WAVE = 1.002f;
        public const float ELITE_HP_MULTIPLIER = 5f;
        public const float BOSS_HP_MULTIPLIER = 25f;

        //Shop Items
        public const int SHOP_REROLL_MAX = 5;
        public const int SHOP_HEALTH_MAX = 5;
        public const int SHOP_GOLD_MULTIPLIER_MAX = 5;
        public const int SHOP_EXP_MULTIPLIER_MAX = 5;
        public const int SHOP_META_MULTIPLIER_MAX = 5;

        public const string GAME_SCENE_NAME = "Game";
    }
}
