using UnityEngine;
using UnityEngine.UIElements;

public class ModelRotator : MonoBehaviour
{
    [Header("References")]
    public UIDocument uiDocument;
    public Transform modelRoot;

    [Header("Settings")]
    [Range(0.1f, 2f)] public float rotationSpeed = 0.3f;

    private bool _isDragging;
    private Vector2 _lastMousePos;
    private VisualElement _viewer;

    void Start()
    {
        _viewer = uiDocument.rootVisualElement.Q<VisualElement>("viewer-container");

        _viewer.RegisterCallback<MouseDownEvent>(OnMouseDown);
        _viewer.RegisterCallback<MouseMoveEvent>(OnMouseMove);
        _viewer.RegisterCallback<MouseUpEvent>(OnMouseUp);
        _viewer.RegisterCallback<MouseLeaveEvent>(_ => _isDragging = false);
    }

    void OnMouseDown(MouseDownEvent e)
    {
        if (e.button != 0)
        {
            return;
        }
        _isDragging = true;
        _lastMousePos = e.localMousePosition;
        _viewer.CaptureMouse();
    }

    void OnMouseMove(MouseMoveEvent e)
    {
        if (!_isDragging || modelRoot == null)
        {
            return;
        }
        float deltaX = e.localMousePosition.x - _lastMousePos.x;
        modelRoot.Rotate(Vector3.up, -deltaX * rotationSpeed, Space.World);
        _lastMousePos = e.localMousePosition;
    }

    void OnMouseUp(MouseUpEvent e)
    {
        _isDragging = false;
        _viewer.ReleaseMouse();
    }
}
