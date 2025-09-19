using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using static UnityEngine.SceneManagement.SceneManager;

namespace Entities {
public sealed class DeathTrigger : MonoBehaviour {
    private TilemapCollider2D collider_2d;

    private void Awake() {
        if (collider_2d == null) Debug.Assert(TryGetComponent(out collider_2d), "TilemapCollider2D component missing!");
        if (collider_2d != null) collider_2d.isTrigger = true;
    }

    private void OnTriggerEnter2D([NotNull] Collider2D other) {
        Debug.Log("Collision detected");
        if (other.gameObject != null && !other.gameObject.CompareTag("Player")) return;

        LoadScene(GetActiveScene().buildIndex, LoadSceneMode.Single);
    }
}
}
