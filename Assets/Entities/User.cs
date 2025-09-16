using UnityEngine;
using UnityEngine.InputSystem;

namespace Entities {
public class User : MonoBehaviour {
    [SerializeField] private InputAction navigate;
    [SerializeField] private InputAction submit;
    [SerializeField] private InputAction cancel;
    [SerializeField] private InputAction point;
    [SerializeField] private InputAction click;
    [SerializeField] private InputAction right_click;
    [SerializeField] private InputAction middle_click;
    [SerializeField] private InputAction scroll;
    [SerializeField] private InputAction tracked_dev_pos;
    [SerializeField] private InputAction tracked_dev_orient;
}
}