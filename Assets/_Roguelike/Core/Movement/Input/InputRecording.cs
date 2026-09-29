using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>
    /// Gravação de input por tick (SPEC §15, RNF-02): grava <see cref="TickInput"/> a partir de um estado inicial e
    /// reproduz como <see cref="IInputSource"/>. Serializa num texto compacto (cabeçalho + 8 hex por tick) para
    /// Temp/Replays/*.json e testes. Depois do fim da gravação, a reprodução devolve input neutro.
    /// </summary>
    public sealed class InputRecording : IInputSource
    {
        private const string FormatTag = "movrec1";

        private readonly List<TickInput> ticks = new List<TickInput>();
        private int cursor;

        /// <summary>Posição dos pés no início da gravação.</summary>
        public Vector2 StartFeet;

        /// <summary>Hash do estado do motor no fim da gravação (0 = não registrado).</summary>
        public ulong FinalHash;

        public int Count => ticks.Count;

        public bool IsFinished => cursor >= ticks.Count;

        public IReadOnlyList<TickInput> Ticks => ticks;

        public void Append(in TickInput input)
        {
            ticks.Add(input);
        }

        public void Clear()
        {
            ticks.Clear();
            cursor = 0;
            FinalHash = 0;
        }

        /// <summary>Volta a reprodução para o primeiro tick.</summary>
        public void Rewind()
        {
            cursor = 0;
        }

        public TickInput NextTick()
        {
            if (cursor >= ticks.Count) return default;
            return ticks[cursor++];
        }

        /// <summary>Texto compacto: "movrec1;x;y;hash;" + 8 dígitos hex por tick.</summary>
        public string Serialize()
        {
            var sb = new StringBuilder(32 + ticks.Count * 8);
            sb.Append(FormatTag).Append(';');
            sb.Append(StartFeet.x.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            sb.Append(StartFeet.y.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            sb.Append(FinalHash.ToString("X16", CultureInfo.InvariantCulture)).Append(';');
            for (int i = 0; i < ticks.Count; i++)
            {
                TickInput t = ticks[i];
                uint packed = (uint)(byte)t.MoveX | ((uint)(byte)t.MoveY << 8) | ((uint)t.Pressed << 16) | ((uint)t.Held << 24);
                sb.Append(packed.ToString("X8", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        public static InputRecording Deserialize(string text)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("Gravação vazia.", nameof(text));

            string[] parts = text.Split(';');
            if (parts.Length != 5 || parts[0] != FormatTag)
            {
                throw new FormatException("Formato de gravação desconhecido.");
            }

            var recording = new InputRecording
            {
                StartFeet = new Vector2(
                    float.Parse(parts[1], CultureInfo.InvariantCulture),
                    float.Parse(parts[2], CultureInfo.InvariantCulture)),
                FinalHash = ulong.Parse(parts[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            };

            string data = parts[4];
            if (data.Length % 8 != 0) throw new FormatException("Dados de tick truncados.");

            for (int i = 0; i < data.Length; i += 8)
            {
                uint packed = uint.Parse(data.Substring(i, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                recording.ticks.Add(new TickInput(
                    (sbyte)(byte)(packed & 0xFF),
                    (sbyte)(byte)((packed >> 8) & 0xFF),
                    (ButtonBits)((packed >> 16) & 0xFF),
                    (ButtonBits)((packed >> 24) & 0xFF)));
            }

            return recording;
        }
    }
}
