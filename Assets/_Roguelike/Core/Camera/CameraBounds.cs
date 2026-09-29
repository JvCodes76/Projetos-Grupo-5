namespace Roguelike.Cameras
{
    /// <summary>
    /// Limites do CENTRO da câmera na fase (CameraBoundary) + plano de queda (RF-50). Sem limites = sem clamp.
    /// </summary>
    public readonly struct CameraBounds
    {
        public CameraBounds(float minX, float maxX, float minY, float maxY, float killPlaneY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
            KillPlaneY = killPlaneY;
            HasBounds = true;
        }

        public float MinX { get; }
        public float MaxX { get; }
        public float MinY { get; }
        public float MaxY { get; }

        /// <summary>A câmera nunca mostra abaixo disto (float.NegativeInfinity = sem plano).</summary>
        public float KillPlaneY { get; }

        public bool HasBounds { get; }

        public static CameraBounds None => default;
    }
}
