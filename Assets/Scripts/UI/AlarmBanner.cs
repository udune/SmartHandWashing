using UnityEngine;
// UnityEngine.UIElements: UI Toolkit 런타임 요소 (VisualElement, Label 등)
using UnityEngine.UIElements;

/// <summary>
/// 비누 교체 예측 배너.
/// AnalyticsPanel의 예측 결과를 10초마다 확인해 배너를 표시/숨김.
/// </summary>
public class AlarmBanner : MonoBehaviour
{
    // ── 상수 정의 ──

    // CheckInterval: 예측 결과 확인 주기 (초)
    private const float CheckInterval = 10f;

    // CSS 클래스명 상수 — UXML/USS에 정의된 클래스와 매칭
    // ClassHidden: 배너 숨김 (display: none)
    private const string ClassHidden = "alarm-hidden";
    // ClassWarn: 경고 스타일 (노란색/주황색)
    private const string ClassWarn   = "alarm-warn";
    // ClassAlert: 긴급 스타일 (빨간색)
    private const string ClassAlert  = "alarm-alert";

    // ── 인스펙터 연결 필드 ──

    // [Header]: 인스펙터에서 필드 그룹 제목 표시
    [Header("References")]
    // uiDocument: UI 트리 루트 접근용
    public UIDocument     uiDocument;
    // analyticsPanel: 예측 결과를 제공하는 컴포넌트
    public AnalyticsPanel analyticsPanel;

    // ── UI 요소 캐시 ──

    // _banner: 알람 배너 컨테이너 (VisualElement)
    private VisualElement _banner;
    // _message: 메인 메시지 레이블 ("D-3 비누 교체 필요")
    private Label         _message;
    // _detail: 상세 정보 레이블 ("일평균 15.2회 사용 기준")
    private Label         _detail;

    // ── 상태 관리 ──

    // _checkTimer: 체크 간격 타이머 (0 → CheckInterval 반복)
    private float      _checkTimer;
    // _currentLevel: 현재 표시 중인 알람 레벨 (클래스 교체 최적화용)
    private AlarmLevel _currentLevel = AlarmLevel.None;

    // Start(): MonoBehaviour 생명주기 - 첫 프레임 전 1회 호출
    void Start()
    {
        // 가드 조건: uiDocument가 연결되지 않았으면 종료
        if (uiDocument == null)
        {
            return;
        }

        // rootVisualElement: UXML 루트에 해당하는 VisualElement
        var root = uiDocument.rootVisualElement;
        // Q<T>("name"): UXML에서 name 속성으로 요소 검색
        _banner  = root.Q<VisualElement>("alarm-banner");
        _message = root.Q<Label>("alarm-message");
        _detail  = root.Q<Label>("alarm-detail");

        // 초기 상태 업데이트
        UpdateBanner();
    }

    // Update(): MonoBehaviour 생명주기 - 매 프레임 호출
    void Update()
    {
        // Time.deltaTime: 이전 프레임 이후 경과 시간 (초)
        _checkTimer += Time.deltaTime;

        // 10초마다 배너 상태 갱신 (매 프레임 호출보다 효율적)
        if (_checkTimer >= CheckInterval)
        {
            _checkTimer = 0f;
            UpdateBanner();
        }
    }

    // UpdateBanner(): 예측 결과에 따라 배너 표시/스타일 갱신
    private void UpdateBanner()
    {
        // null-conditional: analyticsPanel이 null이면 null 반환
        var prediction = analyticsPanel?.GetLastPrediction();

        // 가드 조건: 예측 결과나 배너가 없으면 종료
        if (prediction == null || _banner == null)
        {
            return;
        }

        // AlarmLevel.None: 알람이 필요 없는 상태 → 배너 숨김
        if (prediction.Level == AlarmLevel.None)
        {
            HideBanner();
            return;
        }

        // 레벨 변경 시에만 CSS 클래스 교체 (성능 최적화)
        // DOM 조작은 비용이 크므로 필요할 때만 수행
        if (prediction.Level != _currentLevel)
        {
            // 기존 레벨 클래스 제거
            // RemoveFromClassList(): CSS 클래스 제거
            _banner.RemoveFromClassList(ClassWarn);
            _banner.RemoveFromClassList(ClassAlert);

            // 새 레벨에 맞는 클래스 결정
            string newClass = prediction.Level == AlarmLevel.Alert
                ? ClassAlert  // 긴급: 빨간색
                : ClassWarn;  // 경고: 노란색
            // AddToClassList(): CSS 클래스 추가
            _banner.AddToClassList(newClass);

            // 현재 레벨 상태 저장
            _currentLevel = prediction.Level;
        }

        // 배너 표시 (숨김 클래스 제거)
        _banner.RemoveFromClassList(ClassHidden);

        // 메시지 텍스트 갱신
        if (_message != null)
        {
            // prediction.Message: "D-3 비누 교체 필요" 등
            _message.text = prediction.Message;
        }

        // 상세 정보 텍스트 갱신
        if (_detail != null)
        {
            // :F1 포맷: 소수점 1자리 (예: 15.2)
            _detail.text = $"일평균 {prediction.DailyUsageAvg:F1}회 사용 기준";
        }
    }

    // HideBanner(): 배너를 숨기고 상태 리셋
    private void HideBanner()
    {
        // null-conditional: _banner가 null이면 건너뜀
        // alarm-hidden 클래스 추가 → CSS에서 display: none 처리
        _banner?.AddToClassList(ClassHidden);
        // 상태 리셋
        _currentLevel = AlarmLevel.None;
    }
}
