using System;
using System.Collections.Generic;
using Roguelike.Movement;
using UnityEngine;

/// <summary>
/// Clipes por cue de feedback (SPEC §10.3, RF-57), com volume e variação de pitch (±3–6 %; aleatoriedade só na
/// apresentação). Cue sem clipe = silêncio sem erro (hoje o projeto não tem SFX, AUD P24).
/// Asset: Assets/_Roguelike/Data/Movement/AudioCueSet_Cyborg.asset.
/// </summary>
[CreateAssetMenu(menuName = "Roguelike/Movement/Audio Cue Set", fileName = "AudioCueSet")]
public sealed class AudioCueSet : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public FeedbackCue Cue;
        public AudioClip[] Clips;
        [Range(0f, 1f)] public float Volume;
        [Range(0f, 0.1f)] public float PitchVariation;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;

    public bool TryGet(FeedbackCue cue, out Entry entry)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Cue == cue && entries[i].Clips != null && entries[i].Clips.Length > 0)
            {
                entry = entries[i];
                return true;
            }
        }

        entry = default;
        return false;
    }

    /// <summary>Garante uma entrada (sem clipe) para cada cue (Editor/setup).</summary>
    public void EnsureAllCues()
    {
        foreach (FeedbackCue cue in Enum.GetValues(typeof(FeedbackCue)))
        {
            if (cue == FeedbackCue.None) continue;
            bool found = false;
            for (int i = 0; i < entries.Count; i++) found |= entries[i].Cue == cue;
            if (!found) entries.Add(new Entry { Cue = cue, Clips = Array.Empty<AudioClip>(), Volume = 0.8f, PitchVariation = 0.04f });
        }
    }
}
