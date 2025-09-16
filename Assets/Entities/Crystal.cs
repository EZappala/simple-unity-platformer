using System;
using JetBrains.Annotations;
using UnityEngine;

namespace Entities {
[RequireComponent(typeof(SpriteRenderer), typeof(CapsuleCollider2D))]
public sealed class Crystal : MonoBehaviour {
    private SpriteRenderer sprite_renderer;
    private CapsuleCollider2D collider_2d;
    private Animator anim;

    [SerializeField] private AudioSource pickup_sound;

    // [SerializeField] private AudioSource humming_sound;
    [SerializeField] private float rotation_speed;

    private void Awake() {
        Debug.Assert(TryGetComponent(out sprite_renderer), "Sprite renderer missing!");
        Debug.Assert(TryGetComponent(out collider_2d), "Collider missing!");
        Debug.Assert(TryGetComponent(out anim), "Animator missing!");
        Debug.Assert(pickup_sound != null, "Pickup sound missing!");
        // Debug.Assert(humming_sound != null, "Humming sound missing!");

        anim!.speed = rotation_speed;
        collider_2d!.isTrigger = true;
        // humming_sound.loop = true;
        pickup_sound.loop = false;
    }

    // private void OnEnable() {
        // humming_sound!.Play();
    // }

    private void OnCollisionEnter2D([NotNull] Collision2D other) {
        if (other.gameObject == null || !other.gameObject.CompareTag("Player")) return;

        // anim.SetTrigger("Collect");
        // humming_sound!.Stop();
        pickup_sound!.Play();
        collider_2d!.enabled = false;
        sprite_renderer!.sortingOrder = 10;
        Destroy(gameObject, 0.5f);
    }
}
}
