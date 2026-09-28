using System;
using UnityEngine;

namespace Roguelike.Events
{
    /// <summary>
    /// Barramento estático tipado: existe um barramento independente para cada tipo de evento <typeparamref name="T"/>.
    /// Responsabilidade: entregar fatos de quem emite para quem ouve sem que um conheça o outro.
    /// Quem usa: adaptadores MonoBehaviour (Assembly-CSharp) e o RunManager. A lógica pura dos domínios
    /// (Roguelike.Run, .Upgrades, .Stats, .Levels) NÃO usa o bus; quem a chama é que emite os eventos.
    ///
    /// Contrato (implementação e testes: tarefa 1.2):
    /// - Só na main thread da Unity; não é thread-safe.
    /// - <see cref="Subscribe"/>: handler null lança ArgumentNullException; o mesmo delegate inscrito duas vezes
    ///   fica inscrito uma vez só (idempotente, protege contra OnEnable repetido).
    /// - <see cref="Unsubscribe"/>: handler null lança ArgumentNullException; handler não inscrito é no-op.
    /// - <see cref="Raise"/>: chama os handlers na ordem de inscrição, iterando sobre uma cópia da lista:
    ///     * handler inscrito durante o Raise NÃO é chamado neste Raise (só nos próximos);
    ///     * handler desinscrito durante o Raise e ainda não chamado NÃO é chamado;
    ///     * Raise reentrante (um handler emite outro evento, inclusive do mesmo T) é permitido;
    ///     * exceção num handler vai para Debug.LogException e os handlers seguintes são chamados mesmo assim;
    ///     * sem inscritos é no-op e não aloca (sugestão: array copy-on-write, refeito só em Subscribe/Unsubscribe).
    ///   Com <see cref="EventBusRegistry.LogRaises"/> ligado, loga "[EventBus] - Raise {typeof(T).Name}".
    /// - <see cref="Clear"/>: remove todos os handlers. É internal: só o EventBusRegistry e os testes chamam.
    ///
    /// Registro para limpeza: o construtor estático roda uma única vez por T e por domínio de app, no primeiro
    /// acesso a qualquer membro, e registra <see cref="Clear"/> no <see cref="EventBusRegistry"/>. Um barramento
    /// nunca acessado não tem estado e não precisa de limpeza.
    ///
    /// Regra de uso: MonoBehaviours assinam em OnEnable e desassinam em OnDisable.
    /// </summary>
    public static class EventBus<T> where T : struct, IEvent
    {
        // Array copy-on-write: só é recriado em Subscribe/Unsubscribe. Raise itera sobre a
        // referência atual sem alocar quando não há inscritos (array vazio compartilhado).
        private static Action<T>[] _handlers = Array.Empty<Action<T>>();

        static EventBus()
        {
            EventBusRegistry.Register(typeof(T), Clear);
        }

        /// <summary>Inscreve <paramref name="handler"/> para receber os próximos eventos <typeparamref name="T"/>.</summary>
        public static void Subscribe(Action<T> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (Array.IndexOf(_handlers, handler) >= 0)
            {
                return;
            }

            var next = new Action<T>[_handlers.Length + 1];
            Array.Copy(_handlers, next, _handlers.Length);
            next[_handlers.Length] = handler;
            _handlers = next;
        }

        /// <summary>Remove <paramref name="handler"/>; seguro durante um Raise.</summary>
        public static void Unsubscribe(Action<T> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var index = Array.IndexOf(_handlers, handler);
            if (index < 0)
            {
                return;
            }

            var next = new Action<T>[_handlers.Length - 1];
            Array.Copy(_handlers, 0, next, 0, index);
            Array.Copy(_handlers, index + 1, next, index, _handlers.Length - index - 1);
            _handlers = next;
        }

        /// <summary>Entrega <paramref name="evt"/> a todos os inscritos no momento da chamada (ver regras acima).</summary>
        public static void Raise(in T evt)
        {
            if (EventBusRegistry.LogRaises)
            {
                Debug.Log($"[EventBus] - Raise {typeof(T).Name}");
            }

            // Itera sobre uma cópia (snapshot) para que inscrições feitas durante o Raise só valham
            // a partir do próximo Raise. Antes de cada chamada, confere se o handler ainda está no
            // array atual: se foi desinscrito nesse meio-tempo (por outro handler já chamado), pula.
            var snapshot = _handlers;
            for (int i = 0; i < snapshot.Length; i++)
            {
                var handler = snapshot[i];
                if (Array.IndexOf(_handlers, handler) < 0)
                {
                    continue;
                }

                try
                {
                    handler(evt);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        /// <summary>Remove todos os handlers deste barramento. Chamado pelo EventBusRegistry e pelos testes.</summary>
        internal static void Clear()
        {
            _handlers = Array.Empty<Action<T>>();
        }
    }
}
