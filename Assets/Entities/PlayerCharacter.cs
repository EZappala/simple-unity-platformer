using UnityEngine;

namespace Entities {
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(SpriteRenderer))]
public sealed class PlayerCharacter : MonoBehaviour {
    private Movement2 movement;

    private void Awake() {
        Debug.Assert(TryGetComponent(out movement), "Movement component missing!");
    }
}
}