using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Entities {
public sealed class Movement2 : MonoBehaviour {
    private static readonly int ON_RUN = Animator.StringToHash("OnRun");
    private static readonly int ON_IDLE = Animator.StringToHash("OnIdle");
    private static readonly int ON_JUMP = Animator.StringToHash("OnJump");

    [SerializeField] private InputActionAsset player_ctrl;

    [SerializeField] private InputState input;
    [SerializeField] private float jumping_force = 150f;

    [Header("Gravity")] [SerializeField] private float gravity_normal;
    [SerializeField] private float gravity_fall;
    [SerializeField] private float gravity_jump_cut;
    [SerializeField] private float max_fall_speed;

    [Header("Timings")] [SerializeField] private float coyote_time;

    [Header("Movement")] [SerializeField] private float max_speed;

    [SerializeField] private float collision_tolerance;
    [SerializeField] private float nudge_amount;
    [SerializeField] private float ground_acceleration;
    [SerializeField] private float air_acceleration;
    [SerializeField] private float air_control_multiplier;
    [SerializeField] private float air_max_speed_multiplier;
    [SerializeField] private float ground_friction;

    [Header("Presets")] [SerializeField] private MovementPreset preset = MovementPreset.Snappy;
    [SerializeField] [HideInInspector] private MovementPreset applied_preset = (MovementPreset)(-1);
    private Animator anim;
    private CapsuleCollider2D collider_2d;
    private InputAction jump;

    private float last_grounded_time = -100f;
    private InputAction move;
    private Rigidbody2D rb;
    private SpriteRenderer sprite_renderer;

    private void Awake() {
        Debug.Assert(TryGetComponent(out rb), "Rigidbody2D component missing!");
        Debug.Assert(TryGetComponent(out collider_2d), "CapsuleCollider2D component missing!");
        Debug.Assert(TryGetComponent(out sprite_renderer), "SpriteRenderer component missing!");
        Debug.Assert(TryGetComponent(out anim), "Animator component missing!");
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

    private void FixedUpdate() {
        var bounds = collider_2d!.bounds;
        var cast = Physics2D.CapsuleCast(collider_2d!.bounds.center, bounds.size,
            CapsuleDirection2D.Vertical, 0f, Vector2.down, bounds.extents.y + collision_tolerance,
            LayerMask.GetMask("Ground"));

        input.grounded = cast.collider != null;
        switch (input.grounded) {
            case true: {
                last_grounded_time = Time.time;
                if (input.JumpCanceled) {
                    input.JumpCanceled = false;
                    rb!.gravityScale = gravity_normal;
                }

                break;
            }
            case false when Mathf.Abs(rb!.linearVelocityY) > 0.001f: {
                var hit = Physics2D.Raycast(collider_2d!.bounds.center, Vector2.up,
                    collider_2d.bounds.extents.y + collision_tolerance, LayerMask.GetMask("Ground"));
                if (hit.collider != null) {
                    var nudge = rb!.linearVelocity.x > 0f ? -nudge_amount : nudge_amount;
                    rb!.AddForce(new Vector2(nudge, 0f), ForceMode2D.Impulse);
                }

                break;
            }
        }

        if (input.JumpQueued && Time.time - input.jump_queued_timer <= input.jump_queue_threshold) {
            var within_coyote = Time.time - last_grounded_time <= coyote_time;
            if (input.grounded || within_coyote) {
                rb!.linearVelocity = new Vector2(rb.linearVelocityX, 0f);
                rb.gravityScale = gravity_normal;
                rb.AddForce(Vector2.up * jumping_force, ForceMode2D.Impulse);
                anim!.SetTrigger(ON_JUMP);
                input.JumpQueued = false;
            }
        }

        if (rb!.linearVelocityY < 0f) {
            rb.gravityScale = gravity_fall;
            // TODO: Add fall animation trigger
        }
        else if (input.JumpCanceled && rb.linearVelocityY > 0f) {
            rb.gravityScale = gravity_jump_cut;
            // TODO: Add fall animation trigger
        }
        else rb.gravityScale = gravity_normal;

        // given max_fall_speed is negative, we want to clamp to it if we are falling faster than it
        if (rb.linearVelocityY < max_fall_speed)
            rb.linearVelocity = new Vector2(rb.linearVelocityX, max_fall_speed);

        var input_x = input.move.x;
        if (!input.grounded) input_x *= air_control_multiplier;

        var target_x = input_x * max_speed * (input.grounded ? 1f : air_max_speed_multiplier);
        var new_x = Mathf.MoveTowards(
            rb.linearVelocityX, target_x,
            (input.grounded ? ground_acceleration : air_acceleration) * Time.fixedDeltaTime
        );

        if (input.grounded && Mathf.Abs(input.move.x) <= 0.01f)
            new_x = Mathf.MoveTowards(new_x, 0f, ground_friction * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector2(new_x, rb.linearVelocityY);
        if (input.grounded) {
            switch (Math.Abs(rb.linearVelocityX)) {
                // If moving horizontally on the ground and not already in the run animation, trigger it.
                case > 0.01f when !anim!.GetCurrentAnimatorStateInfo(0).IsName("Run"):
                    anim!.SetTrigger(ON_RUN);
                    break;
                case <= 0.01f when !anim!.GetCurrentAnimatorStateInfo(0).IsName("Idle"):
                    anim!.SetTrigger(ON_IDLE);
                    break;
            }
        }

        if (Mathf.Abs(input.move.x) > 0.01f)
            sprite_renderer!.flipX = input.move.x < 0f;
    }

    private void OnEnable() {
        move!.Enable();
        jump!.Enable();
    }

    private void OnDisable() {
        move!.Disable();
        jump!.Disable();
    }

    private void OnValidate() {
        if (applied_preset == preset) return;

        apply_preset(preset);
        applied_preset = preset;
    }

    public event Action Jumped;
    public event Action<bool> WalkingChanged;

    private void apply_preset(MovementPreset p) {
        switch (p) {
            case MovementPreset.Snappy:
                max_speed = 10f;
                max_fall_speed = -40f;
                ground_acceleration = 300f;
                air_acceleration = 300f;
                air_control_multiplier = 0.55f;
                air_max_speed_multiplier = 1f;
                ground_friction = 50f;
                gravity_fall = 12f;
                gravity_jump_cut = 20f;
                jumping_force = 15;
                gravity_normal = 6f;
                coyote_time = 0.12f;
                break;
            case MovementPreset.Relaxed:
                max_speed = 10f;
                ground_acceleration = 160f;
                air_acceleration = 60f;
                air_control_multiplier = 0.75f;
                air_max_speed_multiplier = 0.8f;
                ground_friction = 30f;
                gravity_fall = 4f;
                gravity_jump_cut = 6f;
                jumping_force = 8;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(p), p, null);
        }
    }

    [ContextMenu("Apply Current Preset")]
    private void apply_current_preset_context_menu() {
        apply_preset(preset);
        applied_preset = preset;
    }

    private void on_jump_canceled(InputAction.CallbackContext _) {
        input.JumpCanceled = true;
    }

    private void on_jump_performed(InputAction.CallbackContext _) {
        input.JumpQueued = true;
        Jumped!.Invoke();
    }

    private void on_move_canceled(InputAction.CallbackContext _) {
        input.move = Vector2.zero;
        WalkingChanged?.Invoke(false);
    }

    private void on_move_performed(InputAction.CallbackContext obj) {
        input.move = obj.ReadValue<Vector2>();
        WalkingChanged?.Invoke(true);
    }
}

[Serializable]
internal struct InputState {
    [SerializeField] internal Vector2 move;

    [SerializeField] internal float jump_queue_threshold;
    internal float jump_queued_timer;
    [SerializeField] private bool jump_queued;

    public bool JumpQueued {
        get => jump_queued;
        set {
            if (value) jump_queued_timer = Time.time;

            jump_queued = value;
        }
    }

    public bool grounded;

    [SerializeField] private bool jump_canceled;

    public bool JumpCanceled {
        get => jump_canceled;
        set {
            jump_canceled = value;
            if (value) jump_queued = false;
        }
    }
}

internal enum MovementPreset {
    Snappy,
    Relaxed
}
}