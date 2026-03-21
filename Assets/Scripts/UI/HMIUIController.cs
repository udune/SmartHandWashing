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
        {
            _viewer3D.style.backgroundImage = Background.FromRenderTexture(viewerRenderTexture);
        }

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
        if (running)
        {
            _soapRemain = 3f;
        }
        else _timerSoap.text = "";
        UpdateGauge(stationController.stationData.soapLevel);
        UpdateStatusLED(stationController.stationData.systemStatus);
    }

    void RefreshWaterUI()
    {
        bool running = stationController.stationData.isWaterRunning;
        SetBtnActive(_btnWater, _ledWater, running);
        if (running)
        {
            _waterRemain = 10f;
        }
        else _timerWater.text = "";
    }

    void RefreshAirUI()
    {
        bool running = stationController.stationData.isAirRunning;
        SetBtnActive(_btnAir, _ledAir, running);
        if (running)
        {
            _airRemain = 10f;
        }
        else _timerAir.text = "";
    }

    void SetBtnActive(VisualElement btn, VisualElement led, bool active)
    {
        if (active)
        {
            btn.AddToClassList("active"); 
            led.AddToClassList("active");
        }
        else
        {
            btn.RemoveFromClassList("active"); 
            led.RemoveFromClassList("active");
        }
    }

    void UpdateGauge(float pct)
    {
        _gaugeFill.style.height = Length.Percent(pct);
        _gaugePctLabel.text = $"{pct:F0}%";

        _gaugeFill.RemoveFromClassList("warning");
        _gaugeFill.RemoveFromClassList("error");
        if (pct <= 0f)
        {
            _gaugeFill.AddToClassList("error");
        }
        else if (pct <= 20f)
        {
            _gaugeFill.AddToClassList("warning");
        }
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
