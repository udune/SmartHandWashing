using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 비누 교체 예측 배너.
/// AnalyticsPanel의 예측 결과를 10초마다 확인해 배너를 표시/숨김.
/// </summary>
public class AlarmBanner : MonoBehaviour
{
    private const float CheckInterval = 10f;
    private const string ClassHidden = "alarm-hidden";
    private const string ClassWarn   = "alarm-warn";
    private const string ClassAlert  = "alarm-alert";

    [Header("References")]
    public UIDocument     uiDocument;
    public AnalyticsPanel analyticsPanel;

    private VisualElement _banner;
    private Label         _message;
    private Label         _detail;

    private float      _checkTimer;
    private AlarmLevel _currentLevel = AlarmLevel.None;

    void Start()
    {
        if (uiDocument == null)
        {
            return;
        }

        var root = uiDocument.rootVisualElement;
        _banner  = root.Q<VisualElement>("alarm-banner");
        _message = root.Q<Label>("alarm-message");
        _detail  = root.Q<Label>("alarm-detail");

        UpdateBanner();
    }

    void Update()
    {
        _checkTimer += Time.deltaTime;

        if (_checkTimer >= CheckInterval)
        {
            _checkTimer = 0f;
            UpdateBanner();
        }
    }

    private void UpdateBanner()
    {
        var prediction = analyticsPanel?.GetLastPrediction();

        if (prediction == null || _banner == null)
        {
            return;
        }

        if (prediction.Level == AlarmLevel.None)
        {
            HideBanner();
            return;
        }

        if (prediction.Level != _currentLevel)
        {
            _banner.RemoveFromClassList(ClassWarn);
            _banner.RemoveFromClassList(ClassAlert);

            string newClass = prediction.Level == AlarmLevel.Alert
                ? ClassAlert
                : ClassWarn;
            _banner.AddToClassList(newClass);

            _currentLevel = prediction.Level;
        }

        _banner.RemoveFromClassList(ClassHidden);

        if (_message != null)
        {
            _message.text = prediction.Message;
        }

        if (_detail != null)
        {
            _detail.text = $"일평균 {prediction.DailyUsageAvg:F1}회 사용 기준";
        }
    }

    private void HideBanner()
    {
        _banner?.AddToClassList(ClassHidden);
        _currentLevel = AlarmLevel.None;
    }
}
