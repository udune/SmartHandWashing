using UnityEngine;
// UnityEngine.UIElements: UI Toolkit API (UXML/USS 기반 UI 시스템)
using UnityEngine.UIElements;
// System: DateTime 클래스 사용
using System;

// HMIUIController: MVC 패턴의 View 역할, UI Toolkit 요소와 데이터 바인딩
public class HMIUIController : MonoBehaviour
{
    // [Header]: Inspector에서 섹션 구분용 라벨 표시
    [Header("References")]
    // UIDocument: UXML 파일이 연결된 UI Toolkit 루트 컴포넌트
    public UIDocument uiDocument;
    public StationController stationController;
    // RenderTexture: 3D 뷰어 카메라의 렌더 타겟 (1024x1024)
    public RenderTexture viewerRenderTexture;

    [Header("Network")]
    public NetworkManager networkManager;   // Inspector에서 연결

    // UI 요소 캐시 - Start()에서 Q<T>()로 검색 후 저장하여 매 프레임 검색 오버헤드 제거
    // Label: 텍스트 표시 요소, VisualElement: 일반 컨테이너/요소
    private Label _datetimeLabel;
    private Label _statusText;
    private VisualElement _statusLed;
    private VisualElement _gaugeFill;
    private Label _gaugePctLabel;
    private VisualElement _btnSoap, _btnWater, _btnAir;
    private Label _timerSoap, _timerWater, _timerAir;
    private VisualElement _ledSoap, _ledWater, _ledAir;
    private VisualElement _viewer3D;

    // 타이머 추적 - 각 동작의 남은 시간 (초)
    private float _soapRemain, _waterRemain, _airRemain;
    // _lastSecond: 1초 간격 갱신용 타이머 (날짜시간 표시)
    private float _lastSecond;

    // Start(): 첫 프레임 전에 호출, UI 요소 캐싱 및 이벤트 바인딩
    void Start()
    {
        // rootVisualElement: UXML 문서의 최상위 요소
        var root = uiDocument.rootVisualElement;

        // Q<T>("name"): UXML에서 name 속성으로 요소 검색 (CSS 셀렉터와 유사)
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
        // Background.FromRenderTexture: RenderTexture를 UI 요소 배경으로 설정
        if (viewerRenderTexture != null)
        {
            _viewer3D.style.backgroundImage = Background.FromRenderTexture(viewerRenderTexture);
        }

        // 버튼 이벤트 (+ PLC 쓰기)
        // RegisterCallback<ClickEvent>: UI Toolkit 클릭 이벤트 리스너 등록
        // 람다의 _ 파라미터: 이벤트 객체 (사용하지 않음)
        _btnSoap.RegisterCallback<ClickEvent>(_ =>
        {
            stationController.ActivateSoap();
            // ?. (null 조건부 연산자): networkManager가 null이면 호출 스킵
            networkManager?.WriteSoapButton(true);
        });

        _btnWater.RegisterCallback<ClickEvent>(_ =>
        {
            stationController.ActivateWater();
            networkManager?.WriteWaterButton(true);
        });

        _btnAir.RegisterCallback<ClickEvent>(_ =>
        {
            stationController.ActivateAir();
            networkManager?.WriteAirButton(true);
        });

        // StationController 이벤트 구독
        // += : C# 이벤트에 델리게이트(메서드 참조) 추가
        stationController.OnSoapUpdated  += RefreshSoapUI;
        stationController.OnWaterUpdated += RefreshWaterUI;
        stationController.OnAirUpdated   += RefreshAirUI;

        // NetworkManager 이벤트 구독
        if (networkManager != null)
        {
            networkManager.OnConnectionChanged += OnNetworkConnectionChanged;
            networkManager.OnStatusChanged     += OnNetworkStatusChanged;
        }

        // 초기 UI 갱신 - 앱 시작 시 현재 상태 반영
        UpdateGauge(stationController.stationData.soapLevel);
        UpdateStatusLED(stationController.stationData.systemStatus);
    }

    // Update(): 매 프레임 호출, 타이머 카운트다운 및 UI 갱신
    void Update()
    {
        // 날짜시간 매 초 갱신 (매 프레임 대신 1초 간격으로 성능 최적화)
        // Time.time: 게임 시작 후 경과 시간 (초)
        if (Time.time - _lastSecond >= 1f)
        {
            _lastSecond = Time.time;
            // DateTime.Now.ToString: 현재 시스템 시간을 포맷 문자열로 변환
            _datetimeLabel.text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        // 타이머 카운트다운 - 동작 중일 때만 감소
        if (stationController.stationData.isSoapRunning)
        {
            // Time.deltaTime: 이전 프레임과의 시간 차이 (초)
            // Mathf.Max(0f, ...): 음수 방지
            _soapRemain = Mathf.Max(0f, _soapRemain - Time.deltaTime);
            // :F1 = 소수점 1자리 포맷, 삼항 연산자로 0이면 빈 문자열
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

        // StationData 변경 감지 (NetworkManager에서 갱신됨)
        // 폴링 방식으로 데이터 변경 반영
        UpdateGauge(stationController.stationData.soapLevel);
        UpdateStatusLED(stationController.stationData.systemStatus);
    }

    // ── Network 콜백 ────────────────────────────────────────────────

    // OnNetworkConnectionChanged: NetworkManager.OnConnectionChanged 이벤트 핸들러
    private void OnNetworkConnectionChanged(bool connected)
    {
        // 연결 상태를 헤더 시스템 LED에 반영
        // connected=false 이면 통신 오류로 처리
        if (!connected)
        {
            // AddToClassList: USS 클래스 추가 (CSS와 유사)
            _statusLed.AddToClassList("warning");
            _statusText.text = "시스템 상태: 통신 오류";
        }
        else
        {
            // 연결 복구 시 실제 시스템 상태로 갱신
            UpdateStatusLED(stationController.stationData.systemStatus);
        }
    }

    // OnNetworkStatusChanged: 네트워크 상태 메시지 로깅
    private void OnNetworkStatusChanged(string msg)
    {
        // 필요 시 화면 하단 등에 상태 메시지 표시
        Debug.Log($"[HMI] 네트워크 상태: {msg}");
    }

    // ── StationController 콜백 ─────────────────────────────────────

    // RefreshSoapUI: StationController.OnSoapUpdated 이벤트 핸들러
    // 비누 동작 시작/종료 시 UI 갱신
    void RefreshSoapUI()
    {
        bool running = stationController.stationData.isSoapRunning;
        SetBtnActive(_btnSoap, _ledSoap, running);
        if (running)
        {
            // 비누 동작 시간: 3초
            _soapRemain = 3f;
        }
        else
        {
            // 동작 종료 시 타이머 텍스트 클리어
            _timerSoap.text = "";
        }
        // 비누 사용으로 잔량 변경 가능 → 게이지/상태 갱신
        UpdateGauge(stationController.stationData.soapLevel);
        UpdateStatusLED(stationController.stationData.systemStatus);
    }

    // RefreshWaterUI: StationController.OnWaterUpdated 이벤트 핸들러
    void RefreshWaterUI()
    {
        bool running = stationController.stationData.isWaterRunning;
        SetBtnActive(_btnWater, _ledWater, running);
        if (running)
        {
            // 물 동작 시간: 10초
            _waterRemain = 10f;
        }
        else
        {
            _timerWater.text = "";
        }
    }

    // RefreshAirUI: StationController.OnAirUpdated 이벤트 핸들러
    void RefreshAirUI()
    {
        bool running = stationController.stationData.isAirRunning;
        SetBtnActive(_btnAir, _ledAir, running);
        if (running)
        {
            // 에어 동작 시간: 10초
            _airRemain = 10f;
        }
        else
        {
            _timerAir.text = "";
        }
    }

    // SetBtnActive: 버튼/LED에 active 클래스 토글
    // USS에서 .active 클래스에 활성화 스타일 정의
    void SetBtnActive(VisualElement btn, VisualElement led, bool active)
    {
        if (active)
        {
            // AddToClassList: USS 클래스 추가
            btn.AddToClassList("active");
            led.AddToClassList("active");
        }
        else
        {
            // RemoveFromClassList: USS 클래스 제거
            btn.RemoveFromClassList("active");
            led.RemoveFromClassList("active");
        }
    }

    // UpdateGauge: 비누 잔량 게이지 바 및 퍼센트 텍스트 갱신
    void UpdateGauge(float pct)
    {
        // Length.Percent(): 퍼센트 단위 길이 값 (0~100)
        // style.height: 인라인 스타일로 높이 설정
        _gaugeFill.style.height = Length.Percent(pct);
        // :F0 = 소수점 없이 정수로 표시
        _gaugePctLabel.text = $"{pct:F0}%";

        // 먼저 모든 상태 클래스 제거 후 조건에 맞는 클래스만 추가
        _gaugeFill.RemoveFromClassList("warning");
        _gaugeFill.RemoveFromClassList("error");
        if (pct <= 0f)
        {
            // 0%: 오류 상태 (빨간색)
            _gaugeFill.AddToClassList("error");
        }
        else if (pct <= 20f)
        {
            // 1~20%: 경고 상태 (노란색)
            _gaugeFill.AddToClassList("warning");
        }
        // 21~100%: 정상 상태 (기본 색상)
    }

    // UpdateStatusLED: 시스템 상태에 따른 LED 색상 및 텍스트 갱신
    void UpdateStatusLED(StationData.SystemStatus status)
    {
        // 먼저 모든 상태 클래스 제거
        _statusLed.RemoveFromClassList("warning");
        _statusLed.RemoveFromClassList("error");
        // switch 문으로 상태별 UI 설정
        switch (status)
        {
            case StationData.SystemStatus.Normal:
                // 정상: 기본 색상 (녹색)
                _statusText.text = "시스템 상태: 정상";
                break;
            case StationData.SystemStatus.Warning:
                // 경고: 노란색
                _statusLed.AddToClassList("warning");
                _statusText.text = "시스템 상태: 주의";
                break;
            case StationData.SystemStatus.Error:
                // 오류: 빨간색
                _statusLed.AddToClassList("error");
                _statusText.text = "시스템 상태: 오류";
                break;
        }
    }

    // OnDestroy(): GameObject 파괴 시 호출, 이벤트 구독 해제
    // 메모리 누수 방지를 위해 반드시 -= 로 구독 해제
    void OnDestroy()
    {
        stationController.OnSoapUpdated  -= RefreshSoapUI;
        stationController.OnWaterUpdated -= RefreshWaterUI;
        stationController.OnAirUpdated   -= RefreshAirUI;

        // NetworkManager 이벤트 구독 해제
        if (networkManager != null)
        {
            networkManager.OnConnectionChanged -= OnNetworkConnectionChanged;
            networkManager.OnStatusChanged     -= OnNetworkStatusChanged;
        }
    }
}
