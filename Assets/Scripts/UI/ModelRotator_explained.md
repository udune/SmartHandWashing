# ModelRotator.cs 코드 설명서

## 개요
- **파일 경로**: `Assets/Scripts/UI/ModelRotator.cs`
- **목적**: UI Toolkit의 3D 뷰어 영역에서 마우스 드래그로 3D 모델을 Y축 회전시키는 상호작용 컨트롤러. Digital Twin에서 손 씻기 스테이션 모델을 다양한 각도로 확인할 수 있게 함.

## 동작 흐름

```
[마우스 다운] → _isDragging = true, 마우스 캡처
      │
      ▼
[마우스 이동] → deltaX 계산 → modelRoot Y축 회전
      │
      ▼
[마우스 업] → _isDragging = false, 마우스 릴리스
```

## 코드 분석

### Line 1-2: using 문
```csharp
using UnityEngine;
using UnityEngine.UIElements;
```
- `UnityEngine`: Transform, Vector3, MonoBehaviour 등
- `UnityEngine.UIElements`: UI Toolkit 이벤트 시스템 (MouseDownEvent 등)

### Line 4-15: 클래스 선언 및 필드
```csharp
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
}
```

| 필드 | 타입 | 용도 |
|------|------|------|
| `uiDocument` | UIDocument | UI Toolkit 문서 (UXML 연결) |
| `modelRoot` | Transform | 회전시킬 3D 모델의 루트 Transform |
| `rotationSpeed` | float | 회전 감도 (0.1~2.0, 기본 0.3) |
| `_isDragging` | bool | 드래그 중 여부 플래그 |
| `_lastMousePos` | Vector2 | 이전 프레임 마우스 위치 (델타 계산용) |
| `_viewer` | VisualElement | 드래그 이벤트를 받는 UI 요소 |

**`[Range(min, max)]` 어트리뷰트:**
- Inspector에서 슬라이더 UI로 표시
- 값 범위를 0.1 ~ 2.0으로 제한

### Line 17-25: Start 메서드
```csharp
void Start()
{
    _viewer = uiDocument.rootVisualElement.Q<VisualElement>("viewer-container");

    _viewer.RegisterCallback<MouseDownEvent>(OnMouseDown);
    _viewer.RegisterCallback<MouseMoveEvent>(OnMouseMove);
    _viewer.RegisterCallback<MouseUpEvent>(OnMouseUp);
    _viewer.RegisterCallback<MouseLeaveEvent>(_ => _isDragging = false);
}
```

**UI 요소 쿼리:**
- `Q<VisualElement>("viewer-container")`: UXML에서 name="viewer-container"인 요소 검색
- 이 요소가 3D 뷰어를 감싸는 컨테이너

**이벤트 등록:**
| 이벤트 | 핸들러 | 동작 |
|--------|--------|------|
| MouseDownEvent | OnMouseDown | 드래그 시작 |
| MouseMoveEvent | OnMouseMove | 모델 회전 |
| MouseUpEvent | OnMouseUp | 드래그 종료 |
| MouseLeaveEvent | 람다 | 영역 이탈 시 드래그 취소 |

### Line 27-36: OnMouseDown 메서드
```csharp
void OnMouseDown(MouseDownEvent e)
{
    if (e.button != 0) return;  // 좌클릭만 처리
    _isDragging = true;
    _lastMousePos = e.localMousePosition;
    _viewer.CaptureMouse();
}
```

**`e.button` 값:**
- 0: 좌클릭
- 1: 우클릭
- 2: 중간 버튼

**`CaptureMouse()`:**
- 마우스 이벤트를 이 요소로 캡처
- 마우스가 요소 영역 밖으로 나가도 이벤트 계속 수신
- 드래그 중 마우스가 UI 밖으로 나가도 회전 유지

**`localMousePosition`:**
- 해당 요소 좌표계 기준 마우스 위치 (좌상단 = 0,0)

### Line 38-47: OnMouseMove 메서드
```csharp
void OnMouseMove(MouseMoveEvent e)
{
    if (!_isDragging || modelRoot == null) return;
    float deltaX = e.localMousePosition.x - _lastMousePos.x;
    modelRoot.Rotate(Vector3.up, -deltaX * rotationSpeed, Space.World);
    _lastMousePos = e.localMousePosition;
}
```

**회전 계산:**
1. `deltaX`: 이전 프레임 대비 X축 이동량 (픽셀)
2. `Rotate(axis, angle, space)`:
   - `Vector3.up`: Y축 (위 방향)
   - `-deltaX * rotationSpeed`: 회전 각도 (마이너스로 자연스러운 방향)
   - `Space.World`: 월드 좌표계 기준 회전

**왜 `-deltaX`인가?**
- 마우스를 오른쪽으로 드래그 → 모델이 왼쪽으로 회전 (반대 방향)
- 이것이 "모델을 잡고 돌리는" 자연스러운 UX

### Line 49-53: OnMouseUp 메서드
```csharp
void OnMouseUp(MouseUpEvent e)
{
    _isDragging = false;
    _viewer.ReleaseMouse();
}
```

**`ReleaseMouse()`:**
- 마우스 캡처 해제
- 다른 UI 요소가 마우스 이벤트를 받을 수 있음

## 좌표계 및 회전

```
        Y (up)
        │
        │
        └───── X (right)
       /
      /
     Z (forward)

마우스 오른쪽 드래그 → deltaX > 0 → -deltaX < 0 → Y축 음의 방향 회전 (시계 방향)
```

## UI Toolkit 마우스 이벤트 API

| API | 설명 |
|-----|------|
| `RegisterCallback<TEvent>(handler)` | 이벤트 리스너 등록 |
| `CaptureMouse()` | 마우스 이벤트 캡처 (드래그용) |
| `ReleaseMouse()` | 마우스 캡처 해제 |
| `e.localMousePosition` | 요소 로컬 좌표계 마우스 위치 |
| `e.button` | 클릭한 마우스 버튼 (0=좌, 1=우, 2=중) |

## 사용 시나리오

1. **설정 예시:**
```
UIRoot (GameObject)
├── UIDocument (Component)
│   └── MainHMI.uxml
│       └── viewer-container (VisualElement)
│
ModelRotator (Script)
├── uiDocument → UIDocument 참조
└── modelRoot → HandWash 모델의 Transform
```

2. **UXML 구조 예시:**
```xml
<ui:VisualElement name="viewer-container">
    <ui:VisualElement name="3d-viewer" />
</ui:VisualElement>
```

## 확장 가능성

| 기능 | 구현 방법 |
|------|----------|
| 수직 회전 | `deltaY` 추가 → X축 회전 |
| 줌 인/아웃 | `MouseWheelEvent` → 카메라 거리 조절 |
| 관성 회전 | 드래그 종료 후 속도 감쇠 적용 |
| 터치 지원 | `PointerDownEvent` 등 사용 |
