using Roguelike.Movement;

namespace Roguelike.MovementPhysics
{
    /// <summary>
    /// Layers do projeto usadas pelo movimento (SPEC §5.2, AUD §3) e a máscara de cada <see cref="QueryLayer"/>.
    /// A layer 7 era "Wall" (sem uso) e virou "KillZone" (DS-13).
    /// </summary>
    public static class MovementLayers
    {
        public const int Default = 0;
        public const int Player = 3;
        public const int Ground = 6;
        public const int KillZone = 7;
        public const int GrappleTarget = 8;
        public const int Decorations = 9;
        public const int Enemy = 10;

        public const int SolidsMask = (1 << Ground) | (1 << Default) | (1 << Enemy);
        public const int GroundMask = 1 << Ground;
        public const int KillZoneMask = 1 << KillZone;
        public const int GrappleTargetMask = 1 << GrappleTarget;

        /// <summary>Tudo menos o próprio jogador: varredura de triggers de tick (EndGoal).</summary>
        public const int TickTriggerMask = ~(1 << Player);

        public static int MaskFor(QueryLayer layer)
        {
            switch (layer)
            {
                case QueryLayer.Solids: return SolidsMask;
                case QueryLayer.WallJumpable:
                case QueryLayer.SafeGround:
                case QueryLayer.GrappleObstacle: return GroundMask;
                case QueryLayer.KillZones: return KillZoneMask;
                default: return 0;
            }
        }

        /// <summary>Consultas de sólidos nunca veem triggers (corrige P04: moedas e EndGoal não cortam o pulo).</summary>
        public static bool UsesTriggers(QueryLayer layer) => layer == QueryLayer.KillZones;
    }
}
