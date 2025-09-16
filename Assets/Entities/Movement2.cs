using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Entities {
/// <inheritdoc />
/// <summary>
/// Recreation of Madeline's movement from Celeste.
/// Old implementation in Movement.cs
/// </summary>
public sealed class Movement2 : MonoBehaviour {
    private Rigidbody2D rb;
    private CapsuleCollider2D collider_2d;
    private SpriteRenderer sprite_renderer;
    private InputAction move;
    private InputAction jump;

    [SerializeField] private InputActionAsset player_ctrl;

    [SerializeField] private InputState input;
    [SerializeField] private float jumping_force = 150f;

    [SerializeField] private float collision_tolerance = 0.005f;
    [SerializeField] private float nudge_amount = 0.1f;

    [SerializeField] private float max_speed = 10f;
    [SerializeField] private float move_speed = 200f;
    [SerializeField] private float horizontal_deceleration = 20f;

    // Gravity tuning, see explanation below
    [SerializeField] private float gravity_normal = 1f;
    [SerializeField] private float gravity_fall = 3.5f;
    [SerializeField] private float gravity_jump_cut = 5f;
    [SerializeField] private float max_fall_speed = -25f;

    // See explanation below
    [SerializeField] private float coyote_time = 0.08f;

    // Horizontal acceleration control
    /// <summary>
    /// Ground Acceleration in units per second^2 (for the MoveTowards function below)
    /// </summary>
    [SerializeField] private float ground_acceleration = 120f;

    [SerializeField] private float air_acceleration = 40f;

    /// <summary>
    /// Reduces the strength of horizontal input while in air to prevent excessive air control.
    /// </summary>
    [SerializeField] private float air_control_multiplier = 0.75f;

    /// <summary>
    /// We don't allow the player to float around and fly by capping max horizontal speed in air.
    /// </summary>
    [SerializeField] private float air_max_speed_multiplier = 0.9f;

    // Keep track of the last time we were grounded for coyote time
    private float last_grounded_time = -100f;

    private void Awake() {
        Debug.Assert(TryGetComponent(out rb), "Rigidbody2D component missing!");
        Debug.Assert(TryGetComponent(out collider_2d), "CapsuleCollider2D component missing!");
        Debug.Assert(TryGetComponent(out sprite_renderer), "SpriteRenderer component missing!");
        Debug.Assert(player_ctrl != null, "Player Input Action Asset not set!");

        var ia = player_ctrl!.FindActionMap("Player", true);
        if (ia != null) {
            move = ia.FindAction("move", true);
            jump = ia.FindAction("jump", true);
        }

        Debug.Assert(move != null, nameof(move) + " != null");
        Debug.Assert(jump != null, nameof(jump) + " != null");

        move.performed += on_move_performed;
        move.canceled += on_move_canceled;
        jump.performed += on_jump_performed;
        jump.canceled += on_jump_canceled;
    }

    private void on_jump_canceled(InputAction.CallbackContext _) {
        input.JumpCanceled = true;
    }

    private void on_jump_performed(InputAction.CallbackContext _) {
        input.JumpQueued = true;
    }

    private void on_move_canceled(InputAction.CallbackContext _) {
        input.move = Vector2.zero;
    }

    private void on_move_performed(InputAction.CallbackContext obj) {
        input.move = obj.ReadValue<Vector2>();
    }

    private void OnEnable() {
        move!.Enable();
        jump!.Enable();
    }

    private void OnDisable() {
        move!.Disable();
        jump!.Disable();
    }

    private void FixedUpdate() {
        // Update grounded state
        var cast = Physics2D.CapsuleCast(collider_2d!.bounds.center, collider_2d.bounds.size, CapsuleDirection2D.Vertical, 0f, Vector2.down, collider_2d.bounds.extents.y + collision_tolerance, LayerMask.GetMask("Ground"));
        var was_grounded = input.grounded;
        input.grounded = cast.collider != null;
        switch (input.grounded) {
            case true: {
                last_grounded_time = Time.time;
                // reset jump-cancel flag and gravity when landing
                if (input.JumpCanceled) {
                    input.JumpCanceled = false;
                    rb!.gravityScale = gravity_normal;
                }

                break;
            }
            // Head nudge when hitting overhead slightly
            case false when Mathf.Abs(rb!.linearVelocity.y) > 0.001f: {
                var hit = Physics2D.Raycast(collider_2d!.bounds.center, Vector2.up, collider_2d.bounds.extents.y + collision_tolerance, LayerMask.GetMask("Ground"));
                if (hit.collider != null) {
                    var nudge = rb!.linearVelocity.x > 0f ? -nudge_amount : nudge_amount;
                    rb!.AddForce(new Vector2(nudge, 0f), ForceMode2D.Impulse);
                }

                break;
            }
        }

        // Jump-buffer + coyote time. Jump-buffering accounts for the player pressing jump just before landing. Essentially
        // we assume that if the player pressed jump within a short time before landing, they meant to jump on landing.
        // Coyote time allows the player to still jump a short time after leaving a platform (named after Wile E. Coyote)
        if (input.JumpQueued && (Time.time - input.jump_queued_timer) <= input.jump_queue_threshold) {
            var within_coyote = (Time.time - last_grounded_time) <= coyote_time;
            if (input.grounded || within_coyote) {
                //  zero vertical velocity for consistent jump
                rb!.linearVelocity = new Vector2(rb.linearVelocityX, 0f);
                rb!.AddForce(Vector2.up * jumping_force, ForceMode2D.Impulse);
                rb!.gravityScale = gravity_normal;
                input.JumpQueued = false;
            }
        }

        // Gravity handling: stronger gravity when falling, and extra when jump is released early
        if (rb!.linearVelocityY < 0f) {
            rb.gravityScale = gravity_fall;
        }
        else if (input.JumpCanceled && rb.linearVelocityY > 0f) {
            rb.gravityScale = gravity_jump_cut;
        }
        else {
            rb.gravityScale = gravity_normal;
        }

        // Clamp fall speed to prevent edge-case issues
        if (rb.linearVelocityY < max_fall_speed) {
            rb.linearVelocity = new Vector2(rb.linearVelocityX, max_fall_speed);
        }

        // Horizontal movement
        // Rather than using the physics engine, we directly lerp velocity for snappy feel.
        // We tune this so that ground movement is more responsive than air.
        // While airborne we reduce input strength and cap max speed.
        var input_x = input.move.x;
        if (!input.grounded) {
            input_x *= air_control_multiplier;
        }

        var target_x = input_x * max_speed * (input.grounded ? 1f : air_max_speed_multiplier);
        var accel = input.grounded ? ground_acceleration : air_acceleration;
        var new_x = Mathf.MoveTowards(rb.linearVelocity.x, target_x, accel * Time.fixedDeltaTime);

        // If there's no input on ground, apply additional friction deceleration towards 0
        if (input.grounded && Mathf.Abs(input.move.x) <= 0.01f) {
            new_x = Mathf.MoveTowards(new_x, 0f, horizontal_deceleration * Time.fixedDeltaTime);
        }

        // Apply computed horizontal velocity and flip sprite based on input or velocity
        rb.linearVelocity = new Vector2(new_x, rb.linearVelocity.y);
        if (Mathf.Abs(input.move.x) > 0.01f) {
            sprite_renderer!.flipX = input.move.x < 0f;
        }
    }
}

[Serializable]
internal struct InputState {
    [SerializeField] internal Vector2 move;

    // Jump-buffering values. note: jump_queue_threshold is in seconds
    [SerializeField] internal float jump_queue_threshold;
    internal float jump_queued_timer;
    [SerializeField] private bool jump_queued;

    public bool JumpQueued {
        get => jump_queued;
        set {
            if (value) {
                jump_queued_timer = Time.time;
            }

            jump_queued = value;
        }
    }

    public bool grounded;

    [SerializeField] private bool jump_canceled;

    public bool JumpCanceled {
        get => jump_canceled;
        set {
            jump_canceled = value;
            if (value) {
                jump_queued = false;
            }
        }
    }
}
}