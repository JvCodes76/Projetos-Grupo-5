using System;

namespace Roguelike.Stats
{
    /// <summary>
    /// Fórmulas de física do pulo extraídas de characterMovement.CalculateJumpVariables
    /// (Assets/scripts/characterMovement.cs ~linha 500), sem alteração de comportamento.
    /// Responsabilidade: só o cálculo puro; a tarefa 2.4 faz o characterMovement usar isto no lugar do código duplicado.
    /// </summary>
    public static class JumpPhysics
    {
        /// <summary>
        /// Velocidade inicial vertical do pulo. Fórmula legada: altura / tempo até o ápice
        /// (não é a cinemática 2h/t; preservada de propósito para não mudar a sensação do pulo).
        /// </summary>
        public static float JumpSpeed(float jumpHeight, float timeToApex)
        {
            if (timeToApex <= 0f) throw new ArgumentOutOfRangeException(nameof(timeToApex));
            return jumpHeight / timeToApex;
        }

        /// <summary>Gravidade (cinemática: h = ½ g t²) necessária para atingir jumpHeight em timeToApex segundos.</summary>
        public static float Gravity(float jumpHeight, float timeToApex)
        {
            if (timeToApex <= 0f) throw new ArgumentOutOfRangeException(nameof(timeToApex));
            return 2f * jumpHeight / (timeToApex * timeToApex);
        }

        /// <summary>Fator para Rigidbody2D.gravityScale que reproduz a gravidade calculada, dada a física global do mundo.</summary>
        public static float GravityMultiplier(float jumpHeight, float timeToApex, float worldGravityMagnitude, float defaultGravityScale)
        {
            if (worldGravityMagnitude <= 0f) throw new ArgumentOutOfRangeException(nameof(worldGravityMagnitude));
            if (defaultGravityScale <= 0f) throw new ArgumentOutOfRangeException(nameof(defaultGravityScale));
            return Gravity(jumpHeight, timeToApex) / worldGravityMagnitude / defaultGravityScale;
        }
    }
}
