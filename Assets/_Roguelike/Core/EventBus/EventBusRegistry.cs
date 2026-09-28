using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Events
{
    /// <summary>
    /// Conhece todos os <see cref="EventBus{T}"/> já usados para poder limpá-los de uma vez.
    /// Descoberta: cada EventBus&lt;T&gt; se registra sozinho no próprio construtor estático via <see cref="Register"/>;
    /// não há reflexão nem lista manual de tipos.
    ///
    /// Invariantes (implementação e testes: tarefa 1.2):
    /// - <see cref="ClearAll"/> roda em <c>SubsystemRegistration</c>, antes de qualquer Awake da primeira cena, e esvazia
    ///   os handlers de todos os barramentos registrados. Isso cobre "Enter Play Mode Options" sem domain reload,
    ///   em que os estáticos sobrevivem entre sessões de Play.
    /// - A lista de barramentos registrados NUNCA é esvaziada: sem domain reload o construtor estático de
    ///   EventBus&lt;T&gt; não roda de novo, então um barramento esquecido aqui nunca mais seria limpo.
    /// - Registrar o mesmo tipo duas vezes não duplica a entrada.
    /// - Handlers inscritos no Editor fora do Play Mode também são removidos ao entrar no Play (comportamento aceito).
    /// </summary>
    public static class EventBusRegistry
    {
        // Chave: tipo do evento. Valor: delegate Clear() daquele EventBus<T>. A ordem de registro
        // não importa para ClearAll (cada barramento é independente), então um Dictionary basta.
        private static readonly Dictionary<Type, Action> _registered = new Dictionary<Type, Action>();

        /// <summary>
        /// Flag de debug. Quando true, todo Raise loga "[EventBus] - Raise NomeDoEvento". Padrão: false.
        /// Não é resetada pelo ClearAll.
        /// </summary>
        public static bool LogRaises { get; set; }

        /// <summary>Tipos de evento cujos barramentos já se registraram (para testes e diagnóstico).</summary>
        internal static IReadOnlyCollection<Type> RegisteredEventTypes => _registered.Keys;

        /// <summary>Chamado pelo construtor estático de EventBus&lt;T&gt;. <paramref name="clear"/> é o Clear daquele barramento.</summary>
        internal static void Register(Type eventType, Action clear)
        {
            _registered[eventType] = clear;
        }

        /// <summary>Remove os handlers de todos os barramentos registrados (mantém o registro).</summary>
        internal static void ClearAll()
        {
            foreach (var clear in _registered.Values)
            {
                clear();
            }
        }

        // Ponto de entrada da Unity ao iniciar o Play Mode (e o player). Não chamar manualmente.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOnSubsystemRegistration()
        {
            ClearAll();
        }
    }
}
