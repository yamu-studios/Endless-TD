// ============================================================================
// ETD.Core - TutorialGates.cs
// Progression gates the sandboxed tutorial uses to hold the player at a step
// until its objective has actually been shown.
//
// Lives in ETD.Core because both sides need it and they cannot reference each
// other: ETD.Gameplay (InGameObjectives, RunManager) already references
// ETD.Turrets (TurretController), so a gate owned by ETD.Gameplay could not be
// read from the turret without a circular assembly dependency.
//
// All gates are wide open by default and are only ever narrowed while a
// tutorial session is running — ResetToDefaults() restores normal play.
// ============================================================================
namespace ETD.Core
{
    public static class TutorialGates
    {
        /// <summary>
        /// False while the tutorial is holding back kill-XP level-ups, so the player
        /// cannot level before the spec-card objective appears.
        /// </summary>
        public static bool LevelUpOpen = true;

        /// <summary>
        /// Highest player level the tutorial permits. The tutorial teaches one
        /// level-up, so it caps at 2 and stops granting XP past that.
        /// </summary>
        public static int PlayerLevelCap = int.MaxValue;

        /// <summary>
        /// Highest level a turret may be upgraded to right now. The tutorial parks the
        /// turret one level below each evolution threshold so the evolution cannot fire
        /// before its objective is on screen — including if the player spams the upgrade
        /// key — then raises the cap when the objective asks them to evolve.
        /// </summary>
        public static int TurretLevelCap = int.MaxValue;

        /// <summary>Restores unrestricted play. Called when a tutorial session ends.</summary>
        public static void ResetToDefaults()
        {
            LevelUpOpen = true;
            PlayerLevelCap = int.MaxValue;
            TurretLevelCap = int.MaxValue;
        }
    }
}
