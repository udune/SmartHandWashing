# MainHMI.uxml 코드 설명서

## 개요
- **파일 경로**: `Assets/UI/UXML/MainHMI.uxml`
- **목적**: HMI(Human-Machine Interface) 화면의 UI 구조를 정의하는 UXML 마크업 파일. HTML과 유사한 계층 구조로 UI 요소를 선언하고, USS 스타일을 적용.

## UXML vs HTML 비교

| UXML | HTML | 설명 |
|------|------|------|
| `<ui:VisualElement>` | `<div>` | 일반 컨테이너 |
| `<ui:Label>` | `<span>` / `<p>` | 텍스트 표시 |
| `<ui:Image>` | `<img>` | 이미지 표시 |
| `name="xxx"` | `id="xxx"` | 요소 식별자 (C#에서 Q("xxx")로 접근) |
| `class="xxx"` | `class="xxx"` | USS 스타일 클래스 |

## UI 레이아웃 구조

```
┌──────────────────────────────────────────────────────────┐
│ root (root-container)                                     │
├──────────────────────────────────────────────────────────┤
│ header (header-bar)                                       │
│  ├─ datetime-label: "2024-01-01 00:00:00"                │
│  └─ status-container                                      │
│      ├─ status-text: "시스템 상태: 정상"                  │
│      └─ status-led: ●                                     │
├──────────────────────────────────────────────────────────┤
│ content-area                                              │
│  ┌───────────┬────────────────────────────────────────┐  │
│  │left-panel │ viewer-container                        │  │
│  │           │                                         │  │
│  │ 비누 잔량 │  viewer-title                          │  │
│  │ 100% ─    │                                         │  │
│  │ ┌──────┐  │  ┌─────────────────────────────────┐   │  │
│  │ │gauge │  │  │                                 │   │  │
│  │ │-track│  │  │        3d-viewer               │   │  │
│  │ │┌────┐│  │  │      (RenderTexture)           │   │  │
│  │ ││fill││  │  │                                 │   │  │
│  │ │└────┘│  │  └─────────────────────────────────┘   │  │
│  │ └──────┘  │                                         │  │
│  │ 0% ─      │                                         │  │
│  │ 100%      │                                         │  │
│  └───────────┴────────────────────────────────────────┘  │
├──────────────────────────────────────────────────────────┤
│ bottom-bar                                                │
│  ┌─────────────────┬─────────────────┬─────────────────┐ │
│  │ btn-soap        │ btn-water       │ btn-air         │ │
│  │ 🧴 비누 [●]     │ 💧 물 [●]       │ 💨 에어 [●]    │ │
│  │   timer-soap    │   timer-water   │   timer-air     │ │
│  └─────────────────┴─────────────────┴─────────────────┘ │
└──────────────────────────────────────────────────────────┘
```

## 코드 분석

### Line 1: UXML 루트 선언
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements"
         xmlns:uie="UnityEditor.UIElements"
         editor-extension-mode="False">
```
- `xmlns:ui="UnityEngine.UIElements"`: 런타임 UI 요소 네임스페이스
- `xmlns:uie="UnityEditor.UIElements"`: 에디터 전용 요소 (사용 안 함)
- `editor-extension-mode="False"`: 런타임 빌드에 포함

### Line 2-3: 스타일시트 연결
```xml
<Style src="project://database/Assets/UI/USS/Variables.uss..."/>
<Style src="project://database/Assets/UI/USS/MainTheme.uss..."/>
```
- `<Style src="...">`: USS 스타일시트 연결
- `project://database/...`: Unity 에셋 경로 형식
- **순서 중요**: Variables.uss가 먼저 로드되어야 MainTheme.uss에서 변수 참조 가능

### Line 4: 루트 컨테이너
```xml
<ui:VisualElement name="root" class="root-container">
```
- `name="root"`: C#에서 `Q<VisualElement>("root")`로 접근
- `class="root-container"`: USS에서 `.root-container { }` 스타일 적용

### Line 5-11: 헤더 영역
```xml
<ui:VisualElement name="header" class="header-bar">
    <ui:Label name="datetime-label" text="2024-01-01 00:00:00" class="header-datetime"/>
    <ui:VisualElement name="status-container" class="status-container">
        <ui:Label name="status-text" text="시스템 상태: 정상" class="status-text"/>
        <ui:VisualElement name="status-led" class="status-led"/>
    </ui:VisualElement>
</ui:VisualElement>
```

| 요소 | name | 용도 |
|------|------|------|
| Label | datetime-label | 현재 날짜/시간 (C#에서 갱신) |
| Label | status-text | 시스템 상태 텍스트 |
| VisualElement | status-led | 상태 LED (클래스로 색상 변경) |

### Line 12-26: 콘텐츠 영역
```xml
<ui:VisualElement name="content-area" class="content-area">
    <!-- 좌측 패널: 비누 게이지 -->
    <ui:VisualElement name="left-panel" class="left-panel">
        <ui:Label text="비누 잔량" class="gauge-label-title"/>
        <ui:VisualElement name="gauge-track" class="gauge-track">
            <ui:VisualElement name="gauge-fill" class="gauge-fill"/>
        </ui:VisualElement>
        <ui:Label name="gauge-pct-label" text="100%" class="gauge-pct-label"/>
    </ui:VisualElement>

    <!-- 중앙: 3D 뷰어 -->
    <ui:VisualElement name="viewer-container" class="viewer-container">
        <ui:VisualElement name="3d-viewer" style="flex-grow: 1; width: 100%;"/>
    </ui:VisualElement>
</ui:VisualElement>
```

**게이지 구조:**
```
gauge-track (배경 바)
└── gauge-fill (채워지는 부분, style.height로 동적 조절)
```

**3D 뷰어:**
- `name="3d-viewer"`: C#에서 RenderTexture를 backgroundImage로 설정
- `style="flex-grow: 1"`: 인라인 스타일로 남은 공간 채움

### Line 27-52: 하단 버튼 바
```xml
<ui:VisualElement name="bottom-bar" class="bottom-bar">
    <!-- 비누 버튼 -->
    <ui:VisualElement name="btn-soap" class="action-btn">
        <ui:Image source="project://...soap.png..."/>
        <ui:VisualElement class="btn-content">
            <ui:Label text="비누" class="btn-name"/>
        </ui:VisualElement>
        <ui:Label name="timer-soap" text="" class="btn-timer"/>
        <ui:VisualElement name="led-soap" class="btn-led"/>
    </ui:VisualElement>

    <!-- 물 버튼 (동일 구조) -->
    <!-- 에어 버튼 (동일 구조) -->
</ui:VisualElement>
```

**버튼 내부 구조:**
```
action-btn
├── Image (아이콘)
├── btn-content
│   └── Label (버튼 이름)
├── Label (timer-xxx, 남은 시간)
└── VisualElement (led-xxx, 동작 LED)
```

### Line 29, 37, 45: 이미지 요소
```xml
<ui:Image source="project://database/Assets/Textures/UI/soap.png?fileID=..."/>
```
- `source="project://..."`: Unity 프로젝트 내 이미지 에셋 경로
- `fileID` / `guid`: Unity 에셋 식별자 (에디터에서 자동 생성)
- `tint-color="rgb(255,255,255)"`: 이미지 틴트 색상 (Line 37)

## 요소 네이밍 규칙

| 접두사 | 용도 | 예시 |
|--------|------|------|
| `btn-` | 버튼 컨테이너 | btn-soap, btn-water |
| `led-` | LED 인디케이터 | led-soap, status-led |
| `timer-` | 타이머 라벨 | timer-soap |
| `gauge-` | 게이지 요소 | gauge-fill, gauge-track |

## C# 연동 예시

```csharp
// Start()에서 요소 캐싱
var root = uiDocument.rootVisualElement;
_datetimeLabel = root.Q<Label>("datetime-label");
_statusLed = root.Q<VisualElement>("status-led");
_gaugeFill = root.Q<VisualElement>("gauge-fill");
_btnSoap = root.Q<VisualElement>("btn-soap");

// 이벤트 등록
_btnSoap.RegisterCallback<ClickEvent>(_ => {
    stationController.ActivateSoap();
});

// 동적 값 변경
_datetimeLabel.text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
_gaugeFill.style.height = Length.Percent(75);
_statusLed.AddToClassList("warning");
```

## 스타일 적용 우선순위

1. **인라인 스타일** (UXML `style=""` 속성)
2. **USS 클래스** (`.class-name { }`)
3. **USS 타입 선택자** (`Label { }`)

```xml
<!-- 인라인 스타일이 USS보다 우선 -->
<ui:VisualElement name="3d-viewer" style="flex-grow: 1; width: 100%;"/>
```

## 에셋 의존성

| 경로 | 용도 |
|------|------|
| `Assets/UI/USS/Variables.uss` | CSS 변수 정의 |
| `Assets/UI/USS/MainTheme.uss` | 전체 스타일 |
| `Assets/Textures/UI/soap.png` | 비누 아이콘 |
| `Assets/Textures/UI/water.png` | 물 아이콘 |
| `Assets/Textures/UI/dry.png` | 에어 드라이 아이콘 |
