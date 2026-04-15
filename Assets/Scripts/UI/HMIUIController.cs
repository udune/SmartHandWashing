using UnityEngine;
using UnityEngine.UIElements;
using System;

public class HMIUIController : MonoBehaviour
{
    [Header("References")]
    public UIDocument uiDocument;
    public StationController stationController;
    public RenderTexture viewerRenderTexture;

    [Header("Network")]
    public NetworkManager networkManager;

    private Label _datetimeLabel;
    private Label _statusText;
    private VisualElement _statusLed;
    private VisualElement _gaugeFill;
    private Label _gaugePctLabel;
    private VisualElement _btnSoap, _btnWater, _btnAir;
    private Label _timerSoap, _timerWater, _timerAir;
    private VisualElement _ledSoap, _ledWater, _ledAir;
    private VisualElement _viewer3D;
    private Button _modeBtn;

    private float _soapRemain, _waterRemain, _airRemain;
    private float _lastSecond;
    private float  _lastSoapLevel = -1f;
    private string _lastStatusText;
    private StationData.SystemStatus _lastLEDStatus = (StationData.SystemStatus)(-1);
    private bool   _networkError;

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

        if (viewerRenderTexture != null)
        {
            _viewer3D.style.backgroundImage = Background.FromRenderTexture(viewerRenderTexture);
        }

        // 모드 토글 버튼 등록
        _modeBtn = root.Q<Button>("btn-mode-toggle");
        if (_modeBtn != null)
        {
            _modeBtn.RegisterCallback<ClickEvent>(_ => ToggleMode());
            UpdateModeBtnLabel();
        }

        _btnSoap.RegisterCallback<ClickEvent>(_ => stationController.RequestSoap());
        _btnWater.RegisterCallback<ClickEvent>(_ => stationController.RequestWater());
        _btnAir.RegisterCallback<ClickEvent>(_ => stationController.RequestAir());

        stationController.OnSoapUpdated  += RefreshSoapUI;
        stationController.OnWaterUpdated += RefreshWaterUI;
        stationController.OnAirUpdated   += RefreshAirUI;

        if (networkManager != null)
        {
            networkManager.OnConnectionChanged += OnNetworkConnectionChanged;
            networkManager.OnStatusChanged     += OnNetworkStatusChanged;
        }

        if (FloorManager.Instance != null)
        {
            FloorManager.Instance.OnFloorChanged += OnFloorChanged;
            RegisterFloorButtons();

            if (FloorManager.Instance.CurrentFloor != null)
            {
                OnFloorChanged(FloorManager.Instance.CurrentFloor);
            }
        }

        UpdateGauge(stationController.stationData.soapLevel);
        UpdateStatusLED(stationController.stationData.systemStatus);
    }

    void Update()
    {
        if (Time.time - _lastSecond >= 1f)
        {
            _lastSecond = Time.time;
            _datetimeLabel.text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        if (stationController.stationData.isSoapRunning && AppModeManager.IsTestMode)
        {
            _soapRemain = Mathf.Max(0f, _soapRemain - Time.deltaTime);
            if (_timerSoap != null)
            {
                _timerSoap.text = _soapRemain > 0 ? $"{_soapRemain:F1}초" : "";
            }
        }
        else if (_timerSoap != null)
        {
            _timerSoap.text = "";
        }

        if (stationController.stationData.isWaterRunning && AppModeManager.IsTestMode)
        {
            _waterRemain = Mathf.Max(0f, _waterRemain - Time.deltaTime);
            if (_timerWater != null)
            {
                _timerWater.text = _waterRemain > 0 ? $"{_waterRemain:F1}초" : "";
            }
        }
        else if (_timerWater != null)
        {
            _timerWater.text = "";
        }

        if (stationController.stationData.isAirRunning && AppModeManager.IsTestMode)
        {
            _airRemain = Mathf.Max(0f, _airRemain - Time.deltaTime);
            if (_timerAir != null)
            {
                _timerAir.text = _airRemain > 0 ? $"{_airRemain:F1}초" : "";
            }
        }
        else if (_timerAir != null)
        {
            _timerAir.text = "";
        }

        if (FloorManager.Instance == null || FloorManager.Instance.IsRealPLCFloor)
        {
            UpdateGauge(stationController.stationData.soapLevel);
        }
        UpdateStatusLED(stationController.stationData.systemStatus);
        UpdateModeDisplay();
    }

    private void UpdateModeDisplay()
    {
        if (stationController?.stationData == null || _statusText == null)
        {
            return;
        }

        string newText;
        if (_networkError)
        {
            newText = "시스템 상태: 통신 오류";
        }
        else
        {
            var data = stationController.stationData;
            string modeStr = data.GetActiveModeName();
            string stepStr = data.currentStep > 0 ? $" - {data.GetCurrentStepName()}" : "";
            string statusStr = data.systemStatus switch
            {
                StationData.SystemStatus.Normal  => "정상",
                StationData.SystemStatus.Warning => "주의",
                StationData.SystemStatus.Error   => "오류",
                _ => "정상"
            };
            newText = $"시스템 상태: {statusStr} [{modeStr}{stepStr}]";
        }

        if (newText != _lastStatusText)
        {
            _lastStatusText = newText;
            _statusText.text = newText;
        }
    }

    private void OnNetworkConnectionChanged(bool connected)
    {
        _networkError = !connected;
        _lastStatusText = null;   // force text redraw next Update
        if (!connected)
        {
            _statusLed.AddToClassList("warning");
        }
        else
        {
            _lastLEDStatus = (StationData.SystemStatus)(-1);   // force LED redraw
        }
    }

    private void OnNetworkStatusChanged(string msg)
    {
        Debug.Log($"[HMI] 네트워크 상태: {msg}");
    }

    void RefreshSoapUI()
    {
        bool running = stationController.stationData.isSoapRunning;
        SetBtnActive(_btnSoap, _ledSoap, running);

        if (running && AppModeManager.IsTestMode)
        {
            _soapRemain = stationController.soapDuration;
        }

        UpdateGauge(stationController.stationData.soapLevel);
        UpdateStatusLED(stationController.stationData.systemStatus);
    }

    void RefreshWaterUI()
    {
        bool running = stationController.stationData.isWaterRunning;
        SetBtnActive(_btnWater, _ledWater, running);

        if (running && AppModeManager.IsTestMode)
        {
            _waterRemain = stationController.waterDuration;
        }
    }

    void RefreshAirUI()
    {
        bool running = stationController.stationData.isAirRunning;
        SetBtnActive(_btnAir, _ledAir, running);

        if (running && AppModeManager.IsTestMode)
        {
            _airRemain = stationController.airDuration;
        }
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
        if (Mathf.Approximately(pct, _lastSoapLevel)) return;
        _lastSoapLevel = pct;

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
        if (status == _lastLEDStatus) return;
        _lastLEDStatus = status;

        _statusLed.RemoveFromClassList("warning");
        _statusLed.RemoveFromClassList("error");
        switch (status)
        {
            case StationData.SystemStatus.Warning:
                _statusLed.AddToClassList("warning");
                break;
            case StationData.SystemStatus.Error:
                _statusLed.AddToClassList("error");
                break;
        }
    }

    private void RegisterFloorButtons()
    {
        var root = uiDocument.rootVisualElement;
        string[] floorIds = { "1F", "2F", "3F", "4F", "5F", "6F", "7F", "8F" };

        foreach (string id in floorIds)
        {
            string capturedId = id;
            var item = root.Q<VisualElement>($"floor-{id.ToLower()}");
            item?.RegisterCallback<ClickEvent>(_ =>
                FloorManager.Instance.SelectFloor(capturedId));
        }
    }

    private void OnFloorChanged(FloorData floor)
    {
        UpdateGauge(floor.soapLevel);
        SetActionButtonsEnabled(floor.isRealPLC);
        UpdateFloorSelection(floor.floorId);
    }

    private void SetActionButtonsEnabled(bool enabled)
    {
        foreach (var btn in new[] { _btnSoap, _btnWater, _btnAir })
        {
            if (btn == null) continue;
            if (enabled)
                btn.RemoveFromClassList("btn-disabled");
            else
                btn.AddToClassList("btn-disabled");
        }
    }

    private void UpdateFloorSelection(string selectedFloorId)
    {
        string[] floorIds = { "1F", "2F", "3F", "4F", "5F", "6F", "7F", "8F" };
        var root = uiDocument.rootVisualElement;

        foreach (string id in floorIds)
        {
            var item = root.Q<VisualElement>($"floor-{id.ToLower()}");
            if (item == null)
            {
                continue;
            }

            if (id == selectedFloorId)
            {
                item.AddToClassList("selected");
            }
            else
            {
                item.RemoveFromClassList("selected");
            }
        }
    }

    private void ToggleMode()
    {
        if (AppModeManager.IsPLCMode)
        {
            AppModeManager.SetMode(AppMode.TEST_MODE);
        }
        else
        {
            AppModeManager.SetMode(AppMode.PLC_MODE);
        }

        UpdateModeBtnLabel();
    }

    private void UpdateModeBtnLabel()
    {
        if (_modeBtn == null)
        {
            return;
        }

        bool isTest = AppModeManager.IsTestMode;
        _modeBtn.text = isTest ? "TEST MODE" : "PLC MODE";
        if (isTest)
        {
            _modeBtn.AddToClassList("mode-test");
        }
        else
        {
            _modeBtn.RemoveFromClassList("mode-test");
        }
    }

    void OnDestroy()
    {
        stationController.OnSoapUpdated  -= RefreshSoapUI;
        stationController.OnWaterUpdated -= RefreshWaterUI;
        stationController.OnAirUpdated   -= RefreshAirUI;

        if (networkManager != null)
        {
            networkManager.OnConnectionChanged -= OnNetworkConnectionChanged;
            networkManager.OnStatusChanged     -= OnNetworkStatusChanged;
        }

        if (FloorManager.Instance != null)
        {
            FloorManager.Instance.OnFloorChanged -= OnFloorChanged;
        }
    }
}
