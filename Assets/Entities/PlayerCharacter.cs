using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using static UnityEngine.Random;
using static UnityEngine.SceneManagement.SceneManager;

namespace Entities {
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(SpriteRenderer))]
public sealed class PlayerCharacter : MonoBehaviour {
    [SerializeField] private AudioSource walk_src;
    [SerializeField] private AudioSource jump_src;
    [SerializeField] private AudioResource walk_sound_audio;
    [SerializeField] private List<AudioResource> jump_sounds;

    [HideInInspector] public uint collected;
    [HideInInspector] public bool needs_respawn;
    private Movement2 movement;

    private Scene scene_to_reload;

    private void Awake() {
        validate();
        if (walk_src != null) walk_src.Stop();
        else throw new UnityException("Walk sound source missing!");

        if (jump_src != null) jump_src.Stop();
        else throw new UnityException("Jump sound source missing!");

        collected = 0;
    }

    private void FixedUpdate() {
        if (transform.position.y > -3f) return;

        LoadScene(GetActiveScene().buildIndex, LoadSceneMode.Single);
    }

    private void OnDestroy() {
        movement!.Jumped -= on_movement_on_jumped;
        movement!.WalkingChanged -= on_movement_on_walking_changed;
    }

    public static event Action<uint> CollectedChanged;

    private void validate() {
        if (movement == null && !TryGetComponent(out movement)) throw new UnityException("Movement component missing!");
        if (walk_src == null) throw new UnityException("Walk sound source missing!");
        if (jump_src == null) throw new UnityException("Jump sound source missing!");

        if (walk_sound_audio == null) throw new UnityException("Pickup sound audio not set!");

        walk_src!.resource = walk_sound_audio;
        walk_src.loop = true;

        if ((jump_sounds ?? throw new UnityException("Invalid operation")).Where(static sound => sound == null).Any())
            throw new UnityException("One of the jump sounds is null!");

        if (movement != null) movement.Jumped += on_movement_on_jumped;
        if (movement != null) movement.WalkingChanged += on_movement_on_walking_changed;
    }


    private void on_movement_on_jumped() {
        if (jump_src == null) throw new UnityException("Jump sound is null");
        if (jump_sounds == null || jump_sounds.Count == 0)
            throw new UnityException("Jump sounds list is null or empty");

        jump_src.resource = jump_sounds[Range(0, jump_sounds.Count)];

        if (jump_src.isPlaying) jump_src.Stop();
        jump_src.loop = false;
        jump_src.Play();
    }

    private void on_movement_on_walking_changed(bool status) {
        if (walk_src == null) throw new UnityException("Walk sound is null");

        walk_src.resource = walk_sound_audio;

        if (status) {
            if (!walk_src.isPlaying) walk_src.Play();
        }
        else {
            if (walk_src.isPlaying) walk_src.Stop();
        }
    }

    internal void OnCrystalPickedUp(bool _) {
        collected++;
        CollectedChanged?.Invoke(collected);
    }
}
}