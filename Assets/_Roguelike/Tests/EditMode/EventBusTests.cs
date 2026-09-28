using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Events;
using UnityEngine;
using UnityEngine.TestTools;

namespace Roguelike.Tests
{
    /// <summary>
    /// Testes de <see cref="EventBus{T}"/> e <see cref="EventBusRegistry"/> (tarefa 1.2).
    /// Usa structs de evento exclusivas deste arquivo para não sujar os barramentos do jogo
    /// (ver Events/GameEvents.cs), e limpa os barramentos usados em SetUp/TearDown.
    /// </summary>
    public class EventBusTests
    {
        private readonly struct TestEventA : IEvent
        {
            public readonly int Value;

            public TestEventA(int value)
            {
                Value = value;
            }
        }

        private readonly struct TestEventB : IEvent
        {
            public readonly int Value;

            public TestEventB(int value)
            {
                Value = value;
            }
        }

        private readonly struct TestEventC : IEvent
        {
            public readonly int Value;

            public TestEventC(int value)
            {
                Value = value;
            }
        }

        // Chave dedicada para testar EventBusRegistry.Register isoladamente, sem tocar em
        // nenhum EventBus<T> real (não implementa IEvent; Register só exige um System.Type).
        private class RegisterDummyKey
        {
        }

        [SetUp]
        public void SetUp()
        {
            EventBus<TestEventA>.Clear();
            EventBus<TestEventB>.Clear();
            EventBus<TestEventC>.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus<TestEventA>.Clear();
            EventBus<TestEventB>.Clear();
            EventBus<TestEventC>.Clear();
            EventBusRegistry.LogRaises = false;
        }

        private static int CountOccurrences(IEnumerable<Type> types, Type target)
        {
            return types.Count(t => t == target);
        }

        [Test]
        public void Raise_DeliversPayloadInSubscriptionOrder()
        {
            var received = new List<int>();
            EventBus<TestEventA>.Subscribe(evt => received.Add(evt.Value * 10));
            EventBus<TestEventA>.Subscribe(evt => received.Add(evt.Value * 100));

            EventBus<TestEventA>.Raise(new TestEventA(3));

            CollectionAssert.AreEqual(new[] { 30, 300 }, received);
        }

        [Test]
        public void Raise_WithoutSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EventBus<TestEventA>.Raise(new TestEventA(1)));
        }

        [Test]
        public void Subscribe_SameHandlerTwice_DeliversOnlyOnce()
        {
            var callCount = 0;
            Action<TestEventA> handler = evt => callCount++;

            EventBus<TestEventA>.Subscribe(handler);
            EventBus<TestEventA>.Subscribe(handler);

            EventBus<TestEventA>.Raise(new TestEventA(1));

            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void Unsubscribe_HandlerNotSubscribed_IsNoOp()
        {
            Action<TestEventA> handler = evt => { };

            Assert.DoesNotThrow(() => EventBus<TestEventA>.Unsubscribe(handler));
        }

        [Test]
        public void Subscribe_NullHandler_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => EventBus<TestEventA>.Subscribe(null));
        }

        [Test]
        public void Unsubscribe_NullHandler_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => EventBus<TestEventA>.Unsubscribe(null));
        }

        [Test]
        public void Raise_HandlerUnsubscribesItself_OtherHandlersStillCalledThisRaise()
        {
            var calls = new List<string>();
            Action<TestEventA> handlerA = null;
            handlerA = evt =>
            {
                calls.Add("A");
                EventBus<TestEventA>.Unsubscribe(handlerA);
            };
            Action<TestEventA> handlerB = evt => calls.Add("B");

            EventBus<TestEventA>.Subscribe(handlerA);
            EventBus<TestEventA>.Subscribe(handlerB);

            EventBus<TestEventA>.Raise(new TestEventA(1));
            CollectionAssert.AreEqual(new[] { "A", "B" }, calls);

            calls.Clear();
            EventBus<TestEventA>.Raise(new TestEventA(2));
            CollectionAssert.AreEqual(new[] { "B" }, calls);
        }

        [Test]
        public void Raise_HandlerUnsubscribesLaterHandler_LaterHandlerNotCalledThisRaise()
        {
            var calls = new List<string>();
            Action<TestEventA> handlerB = evt => calls.Add("B");
            Action<TestEventA> handlerA = evt =>
            {
                calls.Add("A");
                EventBus<TestEventA>.Unsubscribe(handlerB);
            };

            EventBus<TestEventA>.Subscribe(handlerA);
            EventBus<TestEventA>.Subscribe(handlerB);

            EventBus<TestEventA>.Raise(new TestEventA(1));

            CollectionAssert.AreEqual(new[] { "A" }, calls);
        }

        [Test]
        public void Raise_HandlerSubscribedDuringRaise_NotCalledUntilNextRaise()
        {
            var calls = new List<string>();
            Action<TestEventA> handlerB = evt => calls.Add("B");
            Action<TestEventA> handlerA = evt =>
            {
                calls.Add("A");
                EventBus<TestEventA>.Subscribe(handlerB);
            };

            EventBus<TestEventA>.Subscribe(handlerA);

            EventBus<TestEventA>.Raise(new TestEventA(1));
            CollectionAssert.AreEqual(new[] { "A" }, calls);

            calls.Clear();
            EventBus<TestEventA>.Raise(new TestEventA(2));
            CollectionAssert.AreEqual(new[] { "A", "B" }, calls);
        }

        [Test]
        public void Raise_Reentrant_SameEventType_IsAllowed()
        {
            var calls = new List<int>();
            Action<TestEventA> handler = null;
            handler = evt =>
            {
                calls.Add(evt.Value);
                if (evt.Value == 1)
                {
                    EventBus<TestEventA>.Raise(new TestEventA(2));
                }
            };

            EventBus<TestEventA>.Subscribe(handler);
            EventBus<TestEventA>.Raise(new TestEventA(1));

            CollectionAssert.AreEqual(new[] { 1, 2 }, calls);
        }

        [Test]
        public void Raise_Reentrant_DifferentEventType_IsAllowed()
        {
            var calls = new List<string>();
            EventBus<TestEventB>.Subscribe(evt => calls.Add("B"));
            EventBus<TestEventA>.Subscribe(evt =>
            {
                calls.Add("A");
                EventBus<TestEventB>.Raise(new TestEventB(1));
            });

            EventBus<TestEventA>.Raise(new TestEventA(1));

            CollectionAssert.AreEqual(new[] { "A", "B" }, calls);
        }

        [Test]
        public void Raise_HandlerThrows_SubsequentHandlersStillCalledAndExceptionLogged()
        {
            var calls = new List<string>();
            EventBus<TestEventA>.Subscribe(evt => throw new InvalidOperationException("falha proposital de teste"));
            EventBus<TestEventA>.Subscribe(evt => calls.Add("B"));

            // Captura o Debug.LogException num handler próprio em vez de usar LogAssert: assim a exceção
            // proposital não chega ao Console, que precisa ficar com errorCount 0 na receita da §6.1.
            var capture = new CapturingLogHandler();
            var originalHandler = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = capture;
            try
            {
                EventBus<TestEventA>.Raise(new TestEventA(1));
            }
            finally
            {
                Debug.unityLogger.logHandler = originalHandler;
            }

            CollectionAssert.AreEqual(new[] { "B" }, calls);
            Assert.AreEqual(1, capture.Exceptions.Count);
            Assert.IsInstanceOf<InvalidOperationException>(capture.Exceptions[0]);
            Assert.AreEqual("falha proposital de teste", capture.Exceptions[0].Message);
        }

        // Guarda as exceções logadas sem repassá-las ao Console.
        private sealed class CapturingLogHandler : ILogHandler
        {
            public readonly List<Exception> Exceptions = new List<Exception>();

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                Exceptions.Add(exception);
            }

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
            }
        }

        [Test]
        public void Raise_WithLogRaisesEnabled_LogsRaiseMessage()
        {
            EventBusRegistry.LogRaises = true;

            LogAssert.Expect(LogType.Log, "[EventBus] - Raise TestEventA");

            EventBus<TestEventA>.Raise(new TestEventA(1));
        }

        [Test]
        public void Clear_RemovesAllHandlers()
        {
            var callCount = 0;
            EventBus<TestEventA>.Subscribe(evt => callCount++);

            EventBus<TestEventA>.Clear();
            EventBus<TestEventA>.Raise(new TestEventA(1));

            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void EventBusRegistry_ClearAll_ClearsHandlersButKeepsRegistration()
        {
            var callCount = 0;
            EventBus<TestEventC>.Subscribe(evt => callCount++);

            EventBusRegistry.ClearAll();

            CollectionAssert.Contains(EventBusRegistry.RegisteredEventTypes, typeof(TestEventC));

            EventBus<TestEventC>.Raise(new TestEventC(1));
            Assert.AreEqual(0, callCount);

            // Inscrever de novo depois do ClearAll funciona normalmente.
            var secondCallCount = 0;
            EventBus<TestEventC>.Subscribe(evt => secondCallCount++);
            EventBus<TestEventC>.Raise(new TestEventC(2));
            Assert.AreEqual(1, secondCallCount);
        }

        [Test]
        public void EventBusRegistry_Register_SameTypeTwice_DoesNotDuplicateEntry()
        {
            var dummyType = typeof(RegisterDummyKey);

            EventBusRegistry.Register(dummyType, () => { });
            EventBusRegistry.Register(dummyType, () => { });

            Assert.AreEqual(1, CountOccurrences(EventBusRegistry.RegisteredEventTypes, dummyType));
        }
    }
}
