using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 우측 상단 분석 패널 제어.
/// FloorManager와 연동하여 선택된 층의 사용량 데이터를 BarChartElement와 Label에 표시.
/// </summary>
public class AnalyticsPanel : MonoBehaviour
{
    private const int PanelWidth = 320;
    private const int PanelMinHeight = 200;
    private const int PanelOffset = 10;
    private const int BorderRadius = 12;
    private const int BorderWidth = 1;
    private const int Padding = 14;

    private static readonly Color BackgroundColor = new Color(0.05f, 0.12f, 0.18f, 1f);
    private static readonly Color BorderColor = new Color(0f, 0.9f, 0.46f, 1f);

    [Header("References")]
    public UIDocument uiDocument;
    public SoapUsageLogger logger;
    public StationController stationController;

    private VisualElement _panel;
    private BarChartElement _barChart;
    private Label _peakTimeLabel;
    private Label _avgCompareLabel;

    private ReplenishResult _lastPrediction;
    private FloorData _currentFloorData;

    void Start()
    {
        if (uiDocument == null)
        {
            return;
        }

        var root = uiDocument.rootVisualElement;

        _panel = root.Q<VisualElement>("analytics-panel");
        _peakTimeLabel = root.Q<Label>("peak-time-label");
        _avgCompareLabel = root.Q<Label>("avg-compare-label");

        if (_panel == null)
        {
            return;
        }

        ApplyPanelStyles(_panel);
        _panel.BringToFront();

        var chartContainer = root.Q<VisualElement>("bar-chart-container");
        if (chartContainer != null)
        {
            _barChart = new BarChartElement();
            _barChart.style.flexGrow = 1;
            _barChart.style.height = new StyleLength(Length.Percent(100));
            chartContainer.Add(_barChart);
        }

        if (FloorManager.Instance != null)
        {
            FloorManager.Instance.OnFloorChanged += OnFloorChanged;
            if (FloorManager.Instance.CurrentFloor != null)
            {
                OnFloorChanged(FloorManager.Instance.CurrentFloor);
            }
        }

        if (stationController != null)
        {
            stationController.OnSoapUpdated += OnSoapUpdated;
        }
    }

    void OnDestroy()
    {
        if (FloorManager.Instance != null)
        {
            FloorManager.Instance.OnFloorChanged -= OnFloorChanged;
        }
        if (stationController != null)
        {
            stationController.OnSoapUpdated -= OnSoapUpdated;
        }
    }

    private void OnFloorChanged(FloorData floorData)
    {
        _currentFloorData = floorData;
        UpdateChart(_currentFloorData.hourlyUsage);
        RefreshPrediction();
    }

    private void OnSoapUpdated()
    {
        if (_currentFloorData != null && _currentFloorData.isRealPLC && logger != null)
        {
            _barChart?.SetData(logger.HourlyCount, logger.PeakHour);
            UpdateChartLabels(logger.HourlyCount, logger.PeakHour);
            RefreshPrediction();
        }
    }

    private void UpdateChart(int[] hourlyData)
    {
        if (hourlyData == null || _barChart == null)
        {
            return;
        }

        int peakHour = ComputePeakHour(hourlyData);
        _barChart.SetData(hourlyData, peakHour);
        UpdateChartLabels(hourlyData, peakHour);
    }

    private void UpdateChartLabels(int[] hourlyData, int peakHour)
    {
        int todayTotal = hourlyData.Sum();

        if (_peakTimeLabel != null)
        {
            int nextHour = (peakHour + 1) % 24;
            _peakTimeLabel.text = todayTotal > 0 ? $"{peakHour:D2}:00 - {nextHour:D2}:00" : "데이터 없음";
        }

        if (_avgCompareLabel != null)
        {
            double overall = hourlyData.Where(v => v > 0).DefaultIfEmpty(1).Average();
            float peakCount = hourlyData.Length > peakHour ? hourlyData[peakHour] : 0;
            int pct = Mathf.RoundToInt((peakCount / (float)overall) * 100f);
            _avgCompareLabel.text = todayTotal > 0 ? $"전체 평균 대비 {pct}% 사용량" : "데이터 수집 중...";
        }
    }

    private static int ComputePeakHour(int[] hourly)
    {
        if (hourly == null || hourly.Length == 0)
        {
            return 0;
        }
        int peak = 0;
        int maxVal = 0;
        for (int h = 0; h < hourly.Length; h++)
        {
            if (hourly[h] > maxVal)
            {
                maxVal = hourly[h];
                peak = h;
            }
        }
        return peak;
    }

    private void RefreshPrediction()
    {
        if (logger == null || stationController == null)
        {
            return;
        }

        var dailyUsage = logger.GetDailyUsage(logger.Config.prediction.lookbackDays);

        _lastPrediction = ReplenishPredictor.Predict(
            stationController.stationData.soapLevel,
            dailyUsage,
            logger.Config.prediction,
            logger.Config.soapDecreasePerUse
        );
    }

    public ReplenishResult GetLastPrediction()
    {
        return _lastPrediction;
    }

    private void ApplyPanelStyles(VisualElement panel)
    {
        panel.style.position = Position.Absolute;
        panel.style.top = PanelOffset;
        panel.style.right = PanelOffset;
        panel.style.width = PanelWidth;
        panel.style.minHeight = PanelMinHeight;
        panel.style.backgroundColor = BackgroundColor;
        panel.style.borderTopLeftRadius = BorderRadius;
        panel.style.borderTopRightRadius = BorderRadius;
        panel.style.borderBottomLeftRadius = BorderRadius;
        panel.style.borderBottomRightRadius = BorderRadius;
        panel.style.borderTopWidth = BorderWidth;
        panel.style.borderRightWidth = BorderWidth;
        panel.style.borderBottomWidth = BorderWidth;
        panel.style.borderLeftWidth = BorderWidth;
        panel.style.borderTopColor = BorderColor;
        panel.style.borderRightColor = BorderColor;
        panel.style.borderBottomColor = BorderColor;
        panel.style.borderLeftColor = BorderColor;
        panel.style.paddingTop = Padding;
        panel.style.paddingRight = Padding;
        panel.style.paddingBottom = Padding;
        panel.style.paddingLeft = Padding;
    }
}
