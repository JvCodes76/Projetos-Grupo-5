using UnityEngine;

namespace Roguelike.Cameras
{
    /// <summary>
    /// Parâmetros da câmera (SPEC §11, RF-46…RF-51). Asset: Assets/_Roguelike/Data/Movement/CameraProfile_Default.asset.
    /// Sem referência no CameraFollow, ele cria uma instância com estes padrões e loga um aviso.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Movement/Camera Profile", fileName = "CameraProfile")]
    public sealed class CameraProfile : ScriptableObject
    {
        [Header("Área visível (M32, Q8)")]
        [Tooltip("Largura mínima visível (u).")]
        public float MinVisibleWidth = 20f;
        [Tooltip("Altura mínima visível (u). ortho = max(altura/2, largura/2/aspect).")]
        public float MinVisibleHeight = 11.25f;

        [Header("Foco")]
        [Tooltip("Foco acima dos pés (u).")]
        public float FocusOffsetY = 1.5f;
        [Tooltip("Z da câmera.")]
        public float CameraZ = -10f;

        [Header("Suavização (meia-vida, s; RF-46)")]
        public float HalfLifeX = 0.10f;
        public float HalfLifeYUp = 0.20f;
        public float HalfLifeYDown = 0.15f;

        [Header("Horizontal (RF-47, M33)")]
        [Tooltip("Zona morta em X (u).")]
        public float DeadZoneX = 0.5f;
        [Tooltip("Look-ahead = vx × este tempo (s), limitado por LookAheadMax.")]
        public float LookAheadTime = 0.45f;
        public float LookAheadMax = 4.5f;
        [Tooltip("Rampa do look-ahead (u/s).")]
        public float LookAheadRate = 8f;
        [Tooltip("Abaixo desta velocidade o look-ahead fica onde está (u/s).")]
        public float LookAheadMinSpeed = 1f;

        [Header("Vertical (RF-48, RF-49)")]
        [Tooltip("Subir até isto acima do último chão não move a câmera (u).")]
        public float RiseWindow = 4f;
        [Tooltip("Cair mais que isto abaixo do último chão faz a câmera seguir (u).")]
        public float FallFollowDistance = 2.8f;
        [Tooltip("Em queda rápida (vy < −metade da queda máxima), segue depois desta distância (u).")]
        public float FastFallFollowDistance = 0.5f;
        [Tooltip("Ganho do look-down (s): desloca vy acima de metade da queda máxima × ganho.")]
        public float LookDownGain = 0.9f;
        public float LookDownMax = 6.5f;

        [Header("Restrição dura (pés e cabeça sempre na tela)")]
        public float FeetMargin = 1f;
        public float HeadMargin = 1f;

        /// <summary>Tamanho ortográfico que garante a área mínima para o aspecto dado.</summary>
        public float OrthographicSizeFor(float aspect)
        {
            float byHeight = MinVisibleHeight * 0.5f;
            float byWidth = aspect > 0f ? MinVisibleWidth * 0.5f / aspect : byHeight;
            return Mathf.Max(byHeight, byWidth);
        }
    }
}
