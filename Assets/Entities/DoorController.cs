using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Entities {
internal enum Level {
    L0,
    L1,
    L2
}

public sealed class DoorController : MonoBehaviour {
    [SerializeField] private AudioResource deny_sound;
    [SerializeField] private AudioResource success_sound;
    [SerializeField] private AudioSource audio_source;

    private readonly Dictionary<Level, uint> level_requirements = new() {
        { Level.L0, 1 },
        { Level.L1, 3 },
        { Level.L2, 5 }
    };

    private CapsuleCollider2D collider_2d;

    private Level? current_level;
    private CapsuleCollider2D door_trigger;
    private SpriteRenderer sprite_renderer;

    private void Awake() {
        validate();
        validate_level_requirements();
    }

    private void OnTriggerEnter2D([NotNull] Collider2D other) {
        if (!other.gameObject.CompareTag("Player")) return;

        if (current_level == null) {
            var scene_name = SceneManager.GetActiveScene().name;
            if (!Enum.TryParse(scene_name, out Level level)) throw new UnityException("Invalid scene name!");

            current_level = level;
        }

        Debug.Assert(level_requirements != null, nameof(level_requirements) + " != null");
        if (!level_requirements.TryGetValue(current_level.Value, out var required_crystals))
            throw new UnityException("Level not found in requirements dictionary!");

        var player = other.GetComponent<PlayerCharacter>();
        if (player == null) throw new UnityException("PlayerCharacter component missing on player!");

        if (player.collected < required_crystals) {
            if (audio_source == null || audio_source.isPlaying) return;

            audio_source.resource = deny_sound;
            audio_source.loop = false;
            audio_source.Play();
            return;
        }

        switch (current_level) {
            case Level.L0:
                do_change_level(Level.L1);
                break;
            case Level.L1:
                do_change_level(Level.L2);
                break;
            case Level.L2:
                do_change_level(null);
                break;
            default:
                throw new UnityException("Invalid level!");
        }
    }

    private void do_change_level(Level? next_level) {
        if (audio_source != null && !audio_source.isPlaying) {
            audio_source.resource = success_sound;
            audio_source.loop = false;
            audio_source.Play();
        }

        change_level(next_level);
    }

    private void validate() {
        if (sprite_renderer == null) Debug.Assert(TryGetComponent(out sprite_renderer), "Sprite renderer missing!");
        if (audio_source == null) Debug.Assert(TryGetComponent(out audio_source), "Audio source missing!");
        var colliders = GetComponents<CapsuleCollider2D>();
        switch (colliders) {
            case { Length: < 2 }:
                throw new UnityException("Two colliders required!");
            case null:
                return;
        }

        if (colliders[0] == null || colliders[1] == null) throw new UnityException("Colliders missing!");

        door_trigger = colliders[0].isTrigger ? colliders[0] : colliders[1];
        collider_2d = colliders[0].isTrigger ? colliders[1] : colliders[0];
        if (door_trigger == null) Debug.Assert(TryGetComponent(out door_trigger), "Collider missing!");
        if (collider_2d == null) Debug.Assert(TryGetComponent(out collider_2d), "Collider missing!");

        door_trigger!.isTrigger = true;
    }

    // This should only be called when the player finishes the last level
    private static void change_level(Level? level, [CanBeNull] string next_level = "MainMenu") {
        if (level == null && next_level == null)
            throw new UnityException("Both level and next_level cannot be null!");

        SceneManager.LoadScene(level != null ? level.ToString() : next_level);
    }

    private void validate_level_requirements() {
        if (level_requirements == null) return;
        if (current_level == null) return;

        var crystals = FindObjectsByType<Crystal>(FindObjectsSortMode.InstanceID);
        if (crystals == null) return;

        var crystal_count = (uint)crystals.Length;
        if (level_requirements[current_level.Value] == crystal_count) return;

        Debug.LogWarning(
            $"Level {current_level} has {crystal_count} crystals, but requires {level_requirements[current_level.Value]} to open the door. Consider updating the requirements dictionary.",
            this);
        level_requirements[current_level.Value] = crystal_count;
    }
}
}