using UnityEngine;
// UnityEngine.UIElements: UI Toolkit 마우스 이벤트 (MouseDownEvent 등)
using UnityEngine.UIElements;

// ModelRotator: 3D 뷰어에서 마우스 드래그로 모델 Y축 회전
public class ModelRotator : MonoBehaviour
{
    [Header("References")]
    // UIDocument: UXML이 연결된 UI Toolkit 루트 컴포넌트
    public UIDocument uiDocument;
    // modelRoot: 회전시킬 3D 모델의 루트 Transform (HandWash 프리팹)
    public Transform modelRoot;

    [Header("Settings")]
    // [Range]: Inspector에서 슬라이더로 표시, 0.1~2.0 범위 제한
    [Range(0.1f, 2f)] public float rotationSpeed = 0.3f;

    // _isDragging: 마우스 드래그 중 여부 플래그
    private bool _isDragging;
    // _lastMousePos: 이전 프레임 마우스 위치 (deltaX 계산용)
    private Vector2 _lastMousePos;
    // _viewer: 드래그 이벤트를 받는 UI 컨테이너 요소
    private VisualElement _viewer;

    // Start(): UI 요소 검색 및 마우스 이벤트 콜백 등록
    void Start()
    {
        // Q<T>("name"): UXML에서 name 속성으로 요소 검색
        _viewer = uiDocument.rootVisualElement.Q<VisualElement>("viewer-container");

        // RegisterCallback<TEvent>: UI Toolkit 이벤트 리스너 등록
        _viewer.RegisterCallback<MouseDownEvent>(OnMouseDown);
        _viewer.RegisterCallback<MouseMoveEvent>(OnMouseMove);
        _viewer.RegisterCallback<MouseUpEvent>(OnMouseUp);
        // MouseLeaveEvent: 마우스가 요소 영역을 벗어날 때 드래그 취소
        _viewer.RegisterCallback<MouseLeaveEvent>(_ => _isDragging = false);
    }

    // OnMouseDown: 드래그 시작 처리
    void OnMouseDown(MouseDownEvent e)
    {
        // e.button: 0=좌클릭, 1=우클릭, 2=중간 버튼
        if (e.button != 0)
        {
            return;
        }
        _isDragging = true;
        // localMousePosition: 요소 로컬 좌표계 기준 마우스 위치
        _lastMousePos = e.localMousePosition;
        // CaptureMouse(): 마우스 이벤트 캡처 (요소 밖으로 나가도 이벤트 수신)
        _viewer.CaptureMouse();
    }

    // OnMouseMove: 드래그 중 모델 회전
    void OnMouseMove(MouseMoveEvent e)
    {
        if (!_isDragging || modelRoot == null)
        {
            return;
        }
        // deltaX: 이전 프레임 대비 X축 이동량 (픽셀)
        float deltaX = e.localMousePosition.x - _lastMousePos.x;
        // Rotate(축, 각도, 좌표계): Y축(Vector3.up) 기준 월드 좌표계 회전
        // -deltaX: 마우스 오른쪽 드래그 → 모델 왼쪽 회전 (자연스러운 UX)
        modelRoot.Rotate(Vector3.up, -deltaX * rotationSpeed, Space.World);
        _lastMousePos = e.localMousePosition;
    }

    // OnMouseUp: 드래그 종료 처리
    void OnMouseUp(MouseUpEvent e)
    {
        _isDragging = false;
        // ReleaseMouse(): 마우스 캡처 해제
        _viewer.ReleaseMouse();
    }
}
