using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Audio;

namespace Entities {
[RequireComponent(typeof(SpriteRenderer), typeof(CapsuleCollider2D), typeof(AudioSource))]
public sealed class Crystal : MonoBehaviour {
    [SerializeField] private AudioResource pickup_sound_audio;
    [SerializeField] private AudioSource pickup_sound;
    [SerializeField] private float rotation_speed;
    private Animator anim;
    private CapsuleCollider2D collider_2d;
    private bool pickup_fired;
    private SpriteRenderer sprite_renderer;

    private void Awake() {
        validate();
        if (pickup_sound != null) pickup_sound.Stop();
        else throw new UnityException("Failed to get pickup sound");
    }

    private void OnTriggerEnter2D([NotNull] Collider2D other) {
        if (pickup_fired) return;
        if (!other.gameObject.CompareTag("Player")) return;

        if (pickup_sound == null || sprite_renderer == null)
            throw new UnityException("Pickup sound and/or sprite renderer is null");

        if (!pickup_sound.isPlaying) pickup_sound.Play();
        sprite_renderer.enabled = false;
        pickup_fired = true;

        var player = other.gameObject.GetComponent<PlayerCharacter>();
        if (player == null) throw new UnityException("PlayerCharacter is null");

        player.OnCrystalPickedUp(pickup_fired);

        Destroy(gameObject, 0.5f);
    }

    private void validate() {
        if (sprite_renderer == null) Debug.Assert(TryGetComponent(out sprite_renderer), "Sprite renderer missing!");
        if (collider_2d == null) Debug.Assert(TryGetComponent(out collider_2d), "Collider missing!");
        if (anim == null) Debug.Assert(TryGetComponent(out anim), "Animator missing!");
        if (pickup_sound == null) Debug.Assert(TryGetComponent(out pickup_sound), "Pickup sound missing!");
        if (pickup_sound_audio == null) Debug.LogAssertion("Pickup sound audio not set!", this);

        anim!.speed = rotation_speed;
        collider_2d!.isTrigger = true;

        pickup_sound!.resource = pickup_sound_audio;
        pickup_sound.loop = false;
    }
}
}