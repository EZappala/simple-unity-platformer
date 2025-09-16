using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Entities {
public sealed class Movement : MonoBehaviour {
    private Rigidbody2D rb;
    private CapsuleCollider2D collider_2d;
    private SpriteRenderer sprite_renderer;
    private InputAction move;
    private InputAction jump;

    private bool prev_query_start_in_colliders;
    private float prev_gravity;

    public event Action<bool, float> IsGrounded;
    public event Action IsJumping;

    private float frametime;
    private float frame_when_airborne = float.MinValue;
    private float time_jump_pressed;
    private bool grounded;
    private bool jump_pressed;

    // The maximum number of "me's" the character can jump.
    // i.e., madeline jumps 3x her sprite's height.
    [SerializeField] private float jump_force = 17f;
    [SerializeField] private float jump_buf = 0.2f;
    private bool awaiting_jump;
    private bool can_buffer_jumps;
    private bool canceled_jump;
    [SerializeField] private float land_force = -5.22f;
    [SerializeField] private float grav_dampening = 6.875f;

    // [SerializeField] private float max_fall_speed = 10f;
    [SerializeField] private float ground_distance = 0.05f;
    [SerializeField] private float friction_coef_ground = 60f;
    [SerializeField] private float friction_coef_air = 30f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float canceled_jump_gravity_mult = 1.01f;
    [SerializeField] private float max_speed = 7f;
    [SerializeField] private InputActionAsset player_ctrl;

    private Vector2 movement_vector;

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

        move.performed += on_move;
        move.canceled += on_move_canceled;
        jump.performed += on_jump;
        jump.canceled += on_jump_ended;
        jump_pressed = false;
        prev_query_start_in_colliders = Physics2D.queriesStartInColliders;
        prev_gravity = rb.gravityScale;
    }

    private void on_jump(InputAction.CallbackContext obj) {
        // Clac characters jump height, how high can jump compared to own body size.
        // ie n * own_height
        // jump curve, climb, hangtime, fall
        // Celesete: up and down very fast, but with a decent amt of frame time (36 fr)
        //   jumps 3x her own height.
        //   massive amts of air friction, if you stop moving in midair she falls nearly straight down,
        //   air movement is also controllable, however.
        awaiting_jump = true;
        time_jump_pressed = frametime;
    }

    private void on_jump_ended(InputAction.CallbackContext obj) {
        jump_pressed = false;
    }

    private void on_move(InputAction.CallbackContext obj) {
        // A run can be split into 3 parts, acceleration, top_speed, deceleration
        // Celeste: 6fr -> full speed -> stop in 3fr
        //   relatively low top speed, no sprint
        //   changes direction suddenly with no skidding
        //   camera is static, only changing slightly for larger levels, if passing a given threshold.
        movement_vector = obj.ReadValue<Vector2>();
    }

    private void on_move_canceled(InputAction.CallbackContext obj) {
        movement_vector = obj.ReadValue<Vector2>();
    }

    private void OnEnable() {
        Debug.Assert(move != null, nameof(move) + " != null");
        Debug.Assert(jump != null, nameof(jump) + " != null");

        move.Enable();
        jump.Enable();
    }

    private void OnDisable() {
        Debug.Assert(move != null, nameof(move) + " != null");
        Debug.Assert(jump != null, nameof(jump) + " != null");

        move.Disable();
        jump.Disable();
    }

    private void Update() {
        frametime += Time.deltaTime;
    }

    private void FixedUpdate() {
        update_grounded();
        update_jump();
        // update_dir();
        update_jump_forces();
        update_move();
    }

    private void update_grounded() {
        Physics2D.queriesStartInColliders = false;

        bool ground_raycast_hit = Physics2D.CapsuleCast(collider_2d!.bounds.center, collider_2d.size,
            collider_2d.direction, 0, Vector2.down, ground_distance, LayerMask.GetMask("Ground"));
        bool platform_raycast_hit = Physics2D.CapsuleCast(collider_2d!.bounds.center, collider_2d.size,
            collider_2d.direction, 0, Vector2.up, ground_distance, LayerMask.GetMask("Ground"));

        if (platform_raycast_hit) {
            rb!.AddForceY(math.min(0, rb.linearVelocityY), ForceMode2D.Impulse);
        }

        switch (grounded) {
            case false when ground_raycast_hit:
                grounded = true;
                can_buffer_jumps = true;
                canceled_jump = false;
                IsGrounded?.Invoke(true, math.abs(rb!.linearVelocity.y));
                break;
            case true when !ground_raycast_hit:
                grounded = false;
                frame_when_airborne = frametime;
                IsGrounded?.Invoke(false, 0);
                break;
        }

        Physics2D.queriesStartInColliders = prev_query_start_in_colliders;
    }

    private void update_move() {
        // If no movement, apply either ground or air friction to slow down.
        if (movement_vector.x == 0) {
            var friction = grounded ? friction_coef_ground : friction_coef_air;
            rb!.AddForceX(movement_vector.x * friction, ForceMode2D.Impulse);
            return;
        }

        rb!.AddForceX(movement_vector.x * acceleration, ForceMode2D.Impulse);
        rb!.linearVelocityX = math.clamp(rb!.linearVelocityX, -max_speed, max_speed);

        sprite_renderer!.flipX = movement_vector.x switch {
            > 0 => false,
            < 0 => true,
            _ => sprite_renderer!.flipX
        };
    }

    private bool JumpQueued => can_buffer_jumps && frametime < time_jump_pressed + jump_buf;

    private void update_jump() {
        if (!canceled_jump && !grounded && !jump_pressed && rb!.linearVelocityY > 0) {
            canceled_jump = true;
        }

        if (!awaiting_jump && !JumpQueued) return;

        if (grounded) {
            // do jump
            canceled_jump = false;
            time_jump_pressed = 0f;
            can_buffer_jumps = false;
            rb!.AddForceY(jump_force, ForceMode2D.Impulse);
            IsJumping?.Invoke();
        }

        awaiting_jump = false;
    }

    // private void update_dir() {
    //     if (movement_vector.x == 0) {
    //         var friction = grounded ? friction_coef_ground : friction_coef_air;
    //         rb!.AddForceX(Mathf.MoveTowards(rb!.linearVelocityX, 0, friction * Time.fixedDeltaTime),
    //             ForceMode2D.Impulse);
    //         return;
    //     }
    //
    //     rb!.AddForceX(Mathf.MoveTowards(rb!.linearVelocityX, movement_vector.x * max_speed,
    //         acceleration * Time.fixedDeltaTime), ForceMode2D.Impulse);
    // }

    private void update_jump_forces() {
        if (grounded && rb!.linearVelocityY <= 0f) {
            rb!.linearVelocityY = land_force;
            rb!.gravityScale = prev_gravity;
            return;
        }

        var gravity = grav_dampening;

        if (canceled_jump && rb!.linearVelocityY > 0f) {
            gravity *= canceled_jump_gravity_mult;
        }

        rb!.gravityScale = gravity;
    }
}
}