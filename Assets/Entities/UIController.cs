using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Entities {
public sealed class UIController : MonoBehaviour {
    private Button play_button;
    private VisualElement main_element;
    private VisualElement ui;
    [CanBeNull] private DoorController door_controller;

    private void Start() {
        var dc = FindFirstObjectByType<DoorController>();
        if (dc != null) door_controller = dc;

        var ui_cmp = GetComponent<UIDocument>();
        if (ui_cmp == null) throw new UnityException("UI Document component missing!");

        ui = ui_cmp.rootVisualElement;

        if (door_controller == null) {
            play_button = ui.Q<Button>("PlayButton");
            if (play_button != null) play_button.clicked += on_play_clicked;
        }
        else if (door_controller.current_level == Level.L2) {
            main_element = ui.Q<VisualElement>("Main");
            if (main_element is { style: not null }) main_element.style.visibility = Visibility.Hidden;
            DoorController.ChangeLevelRequested += on_door_controller_on_change_level_requested;
        }
    }

    private void on_door_controller_on_change_level_requested(Level? level) {
        if (level != null) return;

        if (main_element is { style: not null }) main_element.style.visibility = Visibility.Visible;
    }

    private void OnDestroy() {
        if (play_button != null) play_button!.clicked -= on_play_clicked;
        DoorController.ChangeLevelRequested -= on_door_controller_on_change_level_requested;
    }

    private static void on_play_clicked() {
        SceneManager.LoadScene("L0");
    }
}
}