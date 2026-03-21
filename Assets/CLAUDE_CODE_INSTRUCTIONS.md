# 스마트 손 씻기 디지털 트윈 — Claude Code 작업 지시서

> **이 문서는 Claude Code 전용입니다.**  
> STEP 4부터 순서대로 구현해주세요. 각 STEP 완료 후 Unity 에디터에서 실행 테스트합니다.

---

## 전제 조건 (사전 완료된 작업)

- Unity 프로젝트명: `SmartHandWashingDT` (URP 템플릿)
- 설치된 패키지: UI Toolkit, Cinemachine, Visual Effect Graph, Input System
- 3D 모델 파일: `Assets/Models/` 에 임포트 완료

---

## STEP 4 — 폴더 구조 및 씬 오브젝트 생성

### 폴더 구조 생성

`Assets/` 아래에 아래 폴더를 생성해줘:

```
Assets/
  Scripts/
    Core/
    UI/
    Network/
    Data/
  UI/
    UXML/
    USS/
  Prefabs/
  RenderTextures/
  Resources/
```

### Main 씬 GameObject 구성

`Main.unity` 씬에 아래 GameObject를 생성해줘:

```
StationManager          (빈 오브젝트) ← StationController.cs 컴포넌트 추가 예정
3DViewer                (빈 오브젝트)
  └─ ModelRoot          ← 3D 모델의 부모 오브젝트
  └─ ViewerCamera       ← RenderTexture 전용 카메라
UIRoot                  ← UIDocument 컴포넌트 부착
ParticleRoot            ← 파티클 시스템들의 부모
```

### ViewerCamera 설정

- `ViewerCamera`에 Camera 컴포넌트 설정:
  - Clear Flags: `Solid Color`, Background: `#0D1B2A`
  - Culling Mask: `3DViewer` 레이어만 체크 (레이어 생성 필요)
  - Target Texture: `Assets/RenderTextures/ViewerRT.renderTexture` 생성 후 연결 (해상도 `1024×1024`, Format `ARGB32`)

---

## STEP 5 — ScriptableObject: StationData.cs

`Assets/Scripts/Data/StationData.cs` 를 아래 내용으로 생성해줘:

```csharp
using UnityEngine;
using System;

[CreateAssetMenu(menuName = "SmartWash/Station Data")]
public class StationData : ScriptableObject
{
    [Header("Soap")]
    [Range(0f, 100f)] public float soapLevel = 100f;
    public int soapUseCount = 0;
    public float soapDecreasePerUse = 5f;

    [Header("Running State")]
    public bool isSoapRunning = false;
    public bool isWaterRunning = false;
    public bool isAirRunning  = false;

    public enum SystemStatus { Normal, Warning, Error }
    public SystemStatus systemStatus = SystemStatus.Normal;

    public event Action OnDataChanged;

    public void UseSoap()
    {
        soapLevel = Mathf.Max(0f, soapLevel - soapDecreasePerUse);
        soapUseCount++;
        if (soapLevel <= 20f && soapLevel > 0f) systemStatus = SystemStatus.Warning;
        if (soapLevel <= 0f)                    systemStatus = SystemStatus.Error;
        OnDataChanged?.Invoke();
    }

    public void ResetData()
    {
        soapLevel     = 100f;
        soapUseCount  = 0;
        isSoapRunning = isWaterRunning = isAirRunning = false;
        systemStatus  = SystemStatus.Normal;
        OnDataChanged?.Invoke();
    }
}
```

작성 후 `Assets/Resources/StationDataInstance.asset` 으로 SO 인스턴스를 생성해줘.  
(`Assets/Resources/` 우클릭 → Create → SmartWash → Station Data)

---

## STEP 6 — UXML + USS 레이아웃

### Variables.uss

`Assets/UI/USS/Variables.uss` 를 아래 내용으로 생성해줘:

```css
:root {
    --color-bg:         #0D1B2A;
    --color-header:     #111E2D;
    --color-panel:      #162032;
    --color-accent:     #2196F3;
    --color-green:      #00E676;
    --color-warning:    #FF9100;
    --color-error:      #FF1744;
    --color-text:       #E0E0E0;
    --color-subtext:    #78909C;
    --color-btn-idle:   #1A2A3A;
    --color-btn-active: #0D47A1;
    --radius-btn:       12px;
}
```

### MainTheme.uss

`Assets/UI/USS/MainTheme.uss` 를 아래 내용으로 생성해줘:

```css
.root-container {
    width: 100%;
    height: 100%;
    flex-direction: column;
    background-color: var(--color-bg);
}

/* ── 헤더 ── */
.header-bar {
    height: 50px;
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    padding: 0 20px;
    background-color: var(--color-header);
    border-bottom-width: 1px;
    border-bottom-color: #1E3A5F;
}
.header-title {
    color: var(--color-text);
    font-size: 16px;
    -unity-font-style: bold;
}
.header-datetime {
    color: var(--color-subtext);
    font-size: 14px;
}
.status-container {
    flex-direction: row;
    align-items: center;
}
.status-text {
    color: var(--color-text);
    font-size: 14px;
    margin-right: 8px;
}
.status-led {
    width: 14px;
    height: 14px;
    border-radius: 7px;
    background-color: var(--color-green);
}
.status-led.warning { background-color: var(--color-warning); }
.status-led.error   { background-color: var(--color-error);   }

/* ── 콘텐츠 영역 ── */
.content-area {
    flex-grow: 1;
    flex-direction: row;
}

/* ── 좌측 게이지 패널 ── */
.left-panel {
    width: 100px;
    background-color: var(--color-panel);
    align-items: center;
    padding: 20px 0;
    border-right-width: 1px;
    border-right-color: #1E3A5F;
}
.gauge-label-title {
    color: var(--color-subtext);
    font-size: 12px;
    margin-bottom: 8px;
}
.gauge-100-label {
    color: var(--color-subtext);
    font-size: 11px;
    margin-bottom: 4px;
}
.gauge-track {
    width: 28px;
    flex-grow: 1;
    background-color: #1A2A3A;
    border-radius: 4px;
    overflow: hidden;
    justify-content: flex-end;
}
.gauge-fill {
    width: 100%;
    background-color: var(--color-green);
    border-radius: 4px;
}
.gauge-fill.warning { background-color: var(--color-warning); }
.gauge-fill.error   { background-color: var(--color-error);   }
.gauge-0-label {
    color: var(--color-subtext);
    font-size: 11px;
    margin-top: 4px;
}
.gauge-pct-label {
    color: var(--color-text);
    font-size: 13px;
    -unity-font-style: bold;
    margin-top: 8px;
}

/* ── 중앙 3D 뷰어 ── */
.viewer-container {
    flex-grow: 1;
    align-items: center;
    justify-content: center;
    background-color: var(--color-bg);
}
.viewer-title {
    position: absolute;
    top: 12px;
    color: var(--color-subtext);
    font-size: 13px;
}

/* ── 하단 버튼 바 ── */
.bottom-bar {
    height: 100px;
    flex-direction: row;
    background-color: var(--color-header);
    border-top-width: 1px;
    border-top-color: #1E3A5F;
    padding: 8px;
}
.action-btn {
    flex-grow: 1;
    flex-direction: row;
    align-items: center;
    justify-content: center;
    background-color: var(--color-btn-idle);
    border-radius: var(--radius-btn);
    margin: 0 6px;
    cursor: hand;
    border-width: 1px;
    border-color: #1E3A5F;
}
.action-btn:hover {
    background-color: #1E3A5F;
}
.action-btn.active {
    background-color: var(--color-btn-active);
    border-color: var(--color-accent);
}
.btn-icon {
    font-size: 28px;
    margin-right: 12px;
}
.btn-content {
    flex-direction: column;
}
.btn-name {
    color: var(--color-text);
    font-size: 16px;
    -unity-font-style: bold;
}
.btn-timer {
    color: var(--color-subtext);
    font-size: 12px;
    margin-top: 2px;
}
.btn-led {
    width: 10px;
    height: 10px;
    border-radius: 5px;
    background-color: #1A2A3A;
    margin-left: 12px;
}
.btn-led.active { background-color: var(--color-green); }
```

### MainHMI.uxml

`Assets/UI/UXML/MainHMI.uxml` 를 아래 내용으로 생성해줘:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <Style src="project://database/Assets/UI/USS/Variables.uss"/>
    <Style src="project://database/Assets/UI/USS/MainTheme.uss"/>

    <ui:VisualElement name="root" class="root-container">

        <!-- 헤더 -->
        <ui:VisualElement name="header" class="header-bar">
            <ui:Label name="title-label" class="header-title" text="스마트 손 씻기 프로젝트 v1.0"/>
            <ui:Label name="datetime-label" class="header-datetime" text="2024-01-01 00:00:00"/>
            <ui:VisualElement name="status-container" class="status-container">
                <ui:Label name="status-text" class="status-text" text="시스템 상태: 정상"/>
                <ui:VisualElement name="status-led" class="status-led"/>
            </ui:VisualElement>
        </ui:VisualElement>

        <!-- 콘텐츠 -->
        <ui:VisualElement name="content-area" class="content-area">

            <!-- 좌측 비누 게이지 -->
            <ui:VisualElement name="left-panel" class="left-panel">
                <ui:Label class="gauge-label-title" text="비누 잔량"/>
                <ui:Label class="gauge-100-label" text="100% ─"/>
                <ui:VisualElement name="gauge-track" class="gauge-track">
                    <ui:VisualElement name="gauge-fill" class="gauge-fill"/>
                </ui:VisualElement>
                <ui:Label class="gauge-0-label" text="0% ─"/>
                <ui:Label name="gauge-pct-label" class="gauge-pct-label" text="100%"/>
            </ui:VisualElement>

            <!-- 중앙 3D 뷰어 -->
            <ui:VisualElement name="viewer-container" class="viewer-container">
                <ui:Label class="viewer-title" text="실시간 디지털 트윈: 스테이션 모델"/>
                <ui:VisualElement name="3d-viewer" style="flex-grow:1; width:100%;"/>
            </ui:VisualElement>

        </ui:VisualElement>

        <!-- 하단 버튼 바 -->
        <ui:VisualElement name="bottom-bar" class="bottom-bar">

            <!-- 비누 버튼 -->
            <ui:VisualElement name="btn-soap" class="action-btn">
                <ui:Label class="btn-icon" text="🧴"/>
                <ui:VisualElement class="btn-content">
                    <ui:Label class="btn-name" text="비누"/>
                    <ui:Label name="timer-soap" class="btn-timer" text=""/>
                </ui:VisualElement>
                <ui:VisualElement name="led-soap" class="btn-led"/>
            </ui:VisualElement>

            <!-- 물 버튼 -->
            <ui:VisualElement name="btn-water" class="action-btn">
                <ui:Label class="btn-icon" text="🚿"/>
                <ui:VisualElement class="btn-content">
                    <ui:Label class="btn-name" text="물"/>
                    <ui:Label name="timer-water" class="btn-timer" text=""/>
                </ui:VisualElement>
                <ui:VisualElement name="led-water" class="btn-led"/>
            </ui:VisualElement>

            <!-- 에어 드라이 버튼 -->
            <ui:VisualElement name="btn-air" class="action-btn">
                <ui:Label class="btn-icon" text="💨"/>
                <ui:VisualElement class="btn-content">
                    <ui:Label class="btn-name" text="에어 드라이"/>
                    <ui:Label name="timer-air" class="btn-timer" text=""/>
                </ui:VisualElement>
                <ui:VisualElement name="led-air" class="btn-led"/>
            </ui:VisualElement>

        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

`UIRoot` GameObject에 `UIDocument` 컴포넌트를 추가하고, Source Asset에 `MainHMI.uxml`을 연결해줘.  
Panel Settings는 `Assets/UI/MainPanelSettings.asset`으로 새로 생성 후 연결.

---

## STEP 7 — C# 스크립트 작성

### StationController.cs

`Assets/Scripts/Core/StationController.cs` 를 생성해줘:

```csharp
using UnityEngine;
using System;
using System.Collections;

public class StationController : MonoBehaviour
{
    [Header("Data")]
    public StationData stationData;

    [Header("Particles")]
    public ParticleSystem soapParticle;
    public ParticleSystem waterParticle;
    public ParticleSystem airParticle;

    public event Action OnSoapUpdated;
    public event Action OnWaterUpdated;
    public event Action OnAirUpdated;

    private Coroutine _soapCoroutine;
    private Coroutine _waterCoroutine;
    private Coroutine _airCoroutine;

    public void ActivateSoap()
    {
        if (stationData.isSoapRunning || stationData.soapLevel <= 0f) return;
        if (_soapCoroutine != null) StopCoroutine(_soapCoroutine);
        _soapCoroutine = StartCoroutine(RunDispenser(
            setter: v => stationData.isSoapRunning = v,
            duration: 3f,
            particle: soapParticle,
            onStart: () => { stationData.UseSoap(); OnSoapUpdated?.Invoke(); },
            onEnd:   () => OnSoapUpdated?.Invoke()
        ));
    }

    public void ActivateWater()
    {
        if (stationData.isWaterRunning) return;
        if (_waterCoroutine != null) StopCoroutine(_waterCoroutine);
        _waterCoroutine = StartCoroutine(RunDispenser(
            setter: v => stationData.isWaterRunning = v,
            duration: 10f,
            particle: waterParticle,
            onStart: () => OnWaterUpdated?.Invoke(),
            onEnd:   () => OnWaterUpdated?.Invoke()
        ));
    }

    public void ActivateAir()
    {
        if (stationData.isAirRunning) return;
        if (_airCoroutine != null) StopCoroutine(_airCoroutine);
        _airCoroutine = StartCoroutine(RunDispenser(
            setter: v => stationData.isAirRunning = v,
            duration: 10f,
            particle: airParticle,
            onStart: () => OnAirUpdated?.Invoke(),
            onEnd:   () => OnAirUpdated?.Invoke()
        ));
    }

    private IEnumerator RunDispenser(
        Action<bool> setter, float duration,
        ParticleSystem particle,
        Action onStart, Action onEnd)
    {
        setter(true);
        if (particle != null) { particle.gameObject.SetActive(true); particle.Play(); }
        onStart?.Invoke();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        setter(false);
        if (particle != null) { particle.Stop(); }
        onEnd?.Invoke();
    }

    // 타이머 남은 시간 조회용 (UI에서 폴링)
    public float GetSoapRemaining()  => GetRemaining(_soapCoroutine,  3f);
    public float GetWaterRemaining() => GetRemaining(_waterCoroutine, 10f);
    public float GetAirRemaining()   => GetRemaining(_airCoroutine,   10f);

    private float GetRemaining(Coroutine c, float total) => c != null ? total : 0f;
}
```

### HMIUIController.cs

`Assets/Scripts/UI/HMIUIController.cs` 를 생성해줘:

```csharp
using UnityEngine;
using UnityEngine.UIElements;
using System;

public class HMIUIController : MonoBehaviour
{
    [Header("References")]
    public UIDocument uiDocument;
    public StationController stationController;
    public RenderTexture viewerRenderTexture;

    // UI 요소 캐시
    private Label _datetimeLabel;
    private Label _statusText;
    private VisualElement _statusLed;
    private VisualElement _gaugeFill;
    private Label _gaugePctLabel;
    private VisualElement _btnSoap, _btnWater, _btnAir;
    private Label _timerSoap, _timerWater, _timerAir;
    private VisualElement _ledSoap, _ledWater, _ledAir;
    private VisualElement _viewer3D;

    // 타이머 추적
    private float _soapRemain, _waterRemain, _airRemain;
    private float _lastSecond;

    void Start()
    {
        var root = uiDocument.rootVisualElement;

        _datetimeLabel = root.Q<Label>("datetime-label");
        _statusText    = root.Q<Label>("status-text");
        _statusLed     = root.Q<VisualElement>("status-led");
        _gaugeFill     = root.Q<VisualElement>("gauge-fill");
        _gaugePctLabel = root.Q<Label>("gauge-pct-label");
        _btnSoap       = root.Q<VisualElement>("btn-soap");
        _btnWater      = root.Q<VisualElement>("btn-water");
        _btnAir        = root.Q<VisualElement>("btn-air");
        _timerSoap     = root.Q<Label>("timer-soap");
        _timerWater    = root.Q<Label>("timer-water");
        _timerAir      = root.Q<Label>("timer-air");
        _ledSoap       = root.Q<VisualElement>("led-soap");
        _ledWater      = root.Q<VisualElement>("led-water");
        _ledAir        = root.Q<VisualElement>("led-air");
        _viewer3D      = root.Q<VisualElement>("3d-viewer");

        // 3D 뷰어 RenderTexture 연결
        if (viewerRenderTexture != null)
            _viewer3D.style.backgroundImage = Background.FromRenderTexture(viewerRenderTexture);

        // 버튼 이벤트
        _btnSoap.RegisterCallback<ClickEvent>(_  => stationController.ActivateSoap());
        _btnWater.RegisterCallback<ClickEvent>(_ => stationController.ActivateWater());
        _btnAir.RegisterCallback<ClickEvent>(_   => stationController.ActivateAir());

        // StationController 이벤트 구독
        stationController.OnSoapUpdated  += RefreshSoapUI;
        stationController.OnWaterUpdated += RefreshWaterUI;
        stationController.OnAirUpdated   += RefreshAirUI;

        // 초기 UI 갱신
        UpdateGauge(stationController.stationData.soapLevel);
        UpdateStatusLED(stationController.stationData.systemStatus);
    }

    void Update()
    {
        // 날짜시간 매 초 갱신
        if (Time.time - _lastSecond >= 1f)
        {
            _lastSecond = Time.time;
            _datetimeLabel.text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        // 타이머 카운트다운
        if (stationController.stationData.isSoapRunning)
        {
            _soapRemain = Mathf.Max(0f, _soapRemain - Time.deltaTime);
            _timerSoap.text = _soapRemain > 0 ? $"{_soapRemain:F1}초" : "";
        }
        if (stationController.stationData.isWaterRunning)
        {
            _waterRemain = Mathf.Max(0f, _waterRemain - Time.deltaTime);
            _timerWater.text = _waterRemain > 0 ? $"{_waterRemain:F1}초" : "";
        }
        if (stationController.stationData.isAirRunning)
        {
            _airRemain = Mathf.Max(0f, _airRemain - Time.deltaTime);
            _timerAir.text = _airRemain > 0 ? $"{_airRemain:F1}초" : "";
        }
    }

    void RefreshSoapUI()
    {
        bool running = stationController.stationData.isSoapRunning;
        SetBtnActive(_btnSoap, _ledSoap, running);
        if (running) _soapRemain = 3f;
        else _timerSoap.text = "";
        UpdateGauge(stationController.stationData.soapLevel);
        UpdateStatusLED(stationController.stationData.systemStatus);
    }

    void RefreshWaterUI()
    {
        bool running = stationController.stationData.isWaterRunning;
        SetBtnActive(_btnWater, _ledWater, running);
        if (running) _waterRemain = 10f;
        else _timerWater.text = "";
    }

    void RefreshAirUI()
    {
        bool running = stationController.stationData.isAirRunning;
        SetBtnActive(_btnAir, _ledAir, running);
        if (running) _airRemain = 10f;
        else _timerAir.text = "";
    }

    void SetBtnActive(VisualElement btn, VisualElement led, bool active)
    {
        if (active) { btn.AddToClassList("active"); led.AddToClassList("active"); }
        else        { btn.RemoveFromClassList("active"); led.RemoveFromClassList("active"); }
    }

    void UpdateGauge(float pct)
    {
        _gaugeFill.style.height = Length.Percent(pct);
        _gaugePctLabel.text = $"{pct:F0}%";

        _gaugeFill.RemoveFromClassList("warning");
        _gaugeFill.RemoveFromClassList("error");
        if (pct <= 0f)       _gaugeFill.AddToClassList("error");
        else if (pct <= 20f) _gaugeFill.AddToClassList("warning");
    }

    void UpdateStatusLED(StationData.SystemStatus status)
    {
        _statusLed.RemoveFromClassList("warning");
        _statusLed.RemoveFromClassList("error");
        switch (status)
        {
            case StationData.SystemStatus.Normal:
                _statusText.text = "시스템 상태: 정상";
                break;
            case StationData.SystemStatus.Warning:
                _statusLed.AddToClassList("warning");
                _statusText.text = "시스템 상태: 주의";
                break;
            case StationData.SystemStatus.Error:
                _statusLed.AddToClassList("error");
                _statusText.text = "시스템 상태: 오류";
                break;
        }
    }

    void OnDestroy()
    {
        stationController.OnSoapUpdated  -= RefreshSoapUI;
        stationController.OnWaterUpdated -= RefreshWaterUI;
        stationController.OnAirUpdated   -= RefreshAirUI;
    }
}
```

### ModelRotator.cs

`Assets/Scripts/UI/ModelRotator.cs` 를 생성해줘:

```csharp
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
        if (e.button != 0) return;
        _isDragging = true;
        _lastMousePos = e.localMousePosition;
        _viewer.CaptureMouse();
    }

    void OnMouseMove(MouseMoveEvent e)
    {
        if (!_isDragging || modelRoot == null) return;
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
```

### 씬 조립

- `UIRoot` GameObject에 `HMIUIController.cs` 추가 후 Inspector에서:
  - `UI Document` → `UIRoot`의 UIDocument 컴포넌트 연결
  - `Station Controller` → `StationManager`의 StationController 연결
  - `Viewer Render Texture` → `ViewerRT.renderTexture` 연결
- `UIRoot` 또는 별도 GameObject에 `ModelRotator.cs` 추가 후:
  - `UI Document` → UIDocument 연결
  - `Model Root` → `ModelRoot` Transform 연결
- `StationManager` GameObject에 `StationController.cs` 추가 후:
  - `Station Data` → `StationDataInstance.asset` 연결

---

## STEP 8 — 파티클 시스템 생성

`ParticleRoot` 하위에 아래 3개의 파티클 시스템을 만들어줘. 기본적으로 `gameObject.SetActive(false)` 상태.

### SoapParticle (비누거품)

```
GameObject: SoapParticle
- SetActive: false
- Particle System:
    Duration: 3 / Looping: true
    Start Lifetime: 1.5~2.5 (랜덤)
    Start Speed: 0.3~0.8
    Start Size: 0.03~0.06
    Start Color: #C8E8FF (반투명, Alpha 180)
    Gravity Modifier: -0.2
  Shape: Cone, Angle 25, Radius 0.05
  Velocity over Lifetime: y=0.5
  Size over Lifetime: 커졌다 작아지는 커브
  Renderer: Material → URP/Particles/Unlit (흰색 원형 스프라이트)
```

### WaterParticle (물)

```
GameObject: WaterParticle
- SetActive: false
- Particle System:
    Duration: 10 / Looping: true
    Start Lifetime: 0.8~1.2
    Start Speed: 1.5~2.5
    Start Size: 0.015~0.03
    Start Color: #4FC3F7 (Alpha 200)
    Gravity Modifier: 2.0
  Shape: Cone, Angle 10, Radius 0.02
  Renderer: Material → URP/Particles/Unlit (파란 원형 스프라이트)
```

### AirParticle (에어 드라이)

```
GameObject: AirParticle
- SetActive: false
- Particle System:
    Duration: 10 / Looping: true
    Start Lifetime: 0.3~0.5
    Start Speed: 3~5
    Start Size: 0.005~0.015
    Start Color: #FFFFFF (Alpha 80)
    Gravity Modifier: 0
  Shape: Box, Scale (0.2, 0.3, 0.1)
  Renderer: Material → URP/Particles/Unlit (흰색 원형 스프라이트)
```

### 파티클 → StationController 연결

`StationManager`의 `StationController` Inspector에서:
- `Soap Particle` → `SoapParticle` 연결
- `Water Particle` → `WaterParticle` 연결
- `Air Particle` → `AirParticle` 연결

---

## 동작 확인 체크리스트

| 항목 | 확인 내용 |
|------|-----------|
| 비누 버튼 클릭 | 3초 카운트다운 + 거품 파티클 + 게이지 감소 |
| 물 버튼 클릭 | 10초 카운트다운 + 물 파티클 |
| 에어 드라이 클릭 | 10초 카운트다운 + 바람 파티클 |
| 비누 20% 이하 | 헤더 LED 주황색 전환 |
| 비누 0% | 헤더 LED 빨간색 + 비누 버튼 비활성 |
| 3D 모델 드래그 | 좌우 회전 동작 |
| 날짜/시간 | 헤더 중앙 실시간 갱신 |

---

## 자주 발생하는 오류

| 오류 | 해결 |
|------|------|
| 모델이 분홍색 | Shader → `Universal Render Pipeline/Lit` 으로 변경 |
| UI가 아무것도 안 보임 | UIDocument → Panel Settings 인스턴스 연결 확인 |
| 3D 뷰어가 검정 | ViewerCamera Target Texture 연결 + Culling Mask 레이어 확인 |
| 드래그 동작 안 함 | Project Settings → Player → Active Input Handling: **Both** 로 변경 |
| 한글 깨짐 (빌드) | Player Settings → Font 에 한글 폰트 (나눔고딕 등) 포함 |
