using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Entities {
public sealed class UIController : MonoBehaviour {
    private Button play_button;
    private VisualElement ui;

    private void Awake() {
        PlayerCharacter.CollectedChanged += on_crystal_picked_up;

        var ui_cmp = GetComponent<UIDocument>();
        if (ui_cmp == null) throw new UnityException("UI Document component missing!");

        ui = ui_cmp.rootVisualElement;
    }

    private void OnEnable() {
        play_button = ui.Q<Button>("PlayButton");
        if (play_button != null) play_button.clicked += on_play_clicked;
    }

    private void OnDestroy() {
        PlayerCharacter.CollectedChanged -= on_crystal_picked_up;
        play_button!.clicked -= on_play_clicked;
    }

    private static void on_play_clicked() {
        SceneManager.LoadScene("L0");
    }

    private void on_crystal_picked_up(uint count) { }
}
}