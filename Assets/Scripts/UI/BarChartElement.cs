// UnityEngine: Vector2, Color 등 기본 타입
using UnityEngine;
// UnityEngine.UIElements: UI Toolkit 런타임 요소
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit 커스텀 VisualElement.
/// painter2D로 24시간 막대 그래프를 직접 드로우.
/// C#에서 동적으로 생성하여 사용 (UXML 호환성 문제 회피)
/// </summary>
public class BarChartElement : VisualElement
{
    // ── 레이아웃 상수 ──

    // HoursPerDay: 하루 시간 수 (막대 개수)
    private const int   HoursPerDay   = 24;
    // PaddingLeft/Right: 좌우 여백 (px)
    private const float PaddingLeft   = 8f;
    private const float PaddingRight  = 8f;
    // PaddingBottom: 하단 여백 — X축 레이블 공간
    private const float PaddingBottom = 20f;
    // PaddingTop: 상단 여백 (px)
    private const float PaddingTop    = 8f;
    // BarGapRatio: 막대 간 간격 비율 (15%)
    private const float BarGapRatio   = 0.15f;
    // AxisLineWidth: X축 선 두께 (px)
    private const float AxisLineWidth = 0.5f;
    // MinBarHeight: 최소 막대 높이 — 0 데이터도 표시
    private const float MinBarHeight  = 1f;

    // ── 색상 상수 ──
    // static readonly: Color는 struct이지만 new로 생성하므로 const 불가

    // BarNormal: 일반 시간대 막대색 (초록)
    private static readonly Color BarNormal = new Color(0.26f, 0.60f, 0.26f, 1f);
    // BarPeak: 피크 시간 막대색 (주황)
    private static readonly Color BarPeak   = new Color(1.00f, 0.57f, 0.00f, 1f);
    // BarAxis: X축 기준선 색상 (회색 50% 투명)
    private static readonly Color BarAxis   = new Color(0.47f, 0.56f, 0.61f, 0.5f);
    // TextColor: 텍스트용 색상 (현재 미사용 — painter2D는 텍스트 미지원)
    private static readonly Color TextColor = new Color(0.47f, 0.56f, 0.61f, 1f);

    // ── 데이터 필드 ──

    // _data: 시간대별 사용량 배열 (0-23시)
    private int[] _data     = new int[HoursPerDay];
    // _peakHour: 피크 시간 인덱스 (-1 = 없음)
    private int   _peakHour = -1;

    // 생성자: VisualElement 렌더링 이벤트에 핸들러 등록
    public BarChartElement()
    {
        // generateVisualContent: VisualElement가 렌더링될 때 발생하는 이벤트
        // 이 이벤트에 핸들러를 등록하면 커스텀 드로잉 가능
        generateVisualContent += OnGenerateVisualContent;
    }

    // SetData(): 외부에서 차트 데이터를 설정하는 공개 API
    // hourlyCount: 24시간 사용량 배열
    // peakHour: 피크 시간 인덱스
    public void SetData(int[] hourlyCount, int peakHour)
    {
        _data     = hourlyCount;
        _peakHour = peakHour;
        // MarkDirtyRepaint(): 다음 프레임에 다시 렌더링하도록 표시
        // 이 호출 없이는 데이터가 변경되어도 화면에 반영되지 않음
        MarkDirtyRepaint();
    }

    // OnGenerateVisualContent(): VisualElement 렌더링 시 호출되는 콜백
    // MeshGenerationContext: 그래픽 드로잉 컨텍스트
    private void OnGenerateVisualContent(MeshGenerationContext ctx)
    {
        // painter2D: HTML5 Canvas 2D와 유사한 벡터 그래픽 API
        var painter = ctx.painter2D;
        // contentRect: 패딩을 제외한 실제 콘텐츠 영역
        float w     = contentRect.width;
        float h     = contentRect.height;

        // 가드 조건: 크기가 0 이하면 드로잉 스킵 (레이아웃 미완료 상태)
        if (w <= 0 || h <= 0)
        {
            return;
        }

        // ── 차트 영역 계산 ──
        // chartW: 막대가 그려지는 실제 너비 (좌우 패딩 제외)
        float chartW = w - PaddingLeft - PaddingRight;
        // chartH: 막대가 그려지는 실제 높이 (상하 패딩 제외)
        float chartH = h - PaddingBottom - PaddingTop;

        // 최댓값: 막대 높이 정규화에 사용 (최소 1 보장)
        int maxVal = GetMaxValue();

        // barW: 각 막대의 너비 (간격 포함)
        float barW   = chartW / HoursPerDay;
        // barGap: 막대 간 간격 (barW의 15%)
        float barGap = barW * BarGapRatio;
        // bottom: 막대의 기준선 Y 좌표 (상단이 0이므로 h - padding)
        float bottom = h - PaddingBottom;

        // ── X축 기준선 드로잉 ──
        // strokeColor: 선 색상 설정
        painter.strokeColor = BarAxis;
        // lineWidth: 선 두께 설정
        painter.lineWidth   = AxisLineWidth;
        // BeginPath(): 새 경로 시작
        painter.BeginPath();
        // MoveTo(): 펜 이동 (그리지 않음)
        painter.MoveTo(new Vector2(PaddingLeft, bottom));
        // LineTo(): 현재 위치에서 선 그림
        painter.LineTo(new Vector2(w - PaddingRight, bottom));
        // Stroke(): 경로를 선으로 그림
        painter.Stroke();

        // ── 막대 드로잉 (24시간 루프) ──
        for (int hour = 0; hour < HoursPerDay; hour++)
        {
            // 막대 높이: (현재값 / 최댓값) × 차트 높이
            // 데이터가 0이면 최소 높이 1px 표시
            float barHeight = _data[hour] > 0
                ? (float)_data[hour] / maxVal * chartH
                : MinBarHeight;

            // x: 막대 좌측 X 좌표 (간격의 절반을 왼쪽에 추가하여 중앙 정렬)
            float x = PaddingLeft + hour * barW + barGap * 0.5f;
            // y: 막대 상단 Y 좌표 (아래에서 위로 성장)
            float y = bottom - barHeight;

            // 피크 시간이면 주황색, 아니면 초록색
            painter.fillColor = (hour == _peakHour && _data[hour] > 0)
                ? BarPeak
                : BarNormal;

            // 막대 사각형 그리기 (시계방향 4점)
            painter.BeginPath();
            // 좌하단 → 좌상단 → 우상단 → 우하단
            painter.MoveTo(new Vector2(x,                 bottom));
            painter.LineTo(new Vector2(x,                 y));
            painter.LineTo(new Vector2(x + barW - barGap, y));
            painter.LineTo(new Vector2(x + barW - barGap, bottom));
            // ClosePath(): 시작점으로 경로 닫기
            painter.ClosePath();
            // Fill(): 내부 채우기
            painter.Fill();
        }

        // ── X축 레이블 (0, 6, 12, 18, 24) ───────────────────────────
        // painter2D는 텍스트를 지원하지 않으므로
        // 레이블은 UXML의 Label 엘리먼트로 별도 처리 (AnalyticsPanel.cs 참조)
    }

    // GetMaxValue(): 배열에서 최댓값 찾기 (정규화용)
    // 반환값: 최소 1 (0 나누기 방지)
    private int GetMaxValue()
    {
        // 최솟값 1로 시작 — 0 나누기 방지
        int maxVal = 1;

        // O(n) 선형 탐색 — LINQ Max() 대신 수동 루프 (GC 부담 감소)
        foreach (var v in _data)
        {
            if (v > maxVal)
            {
                maxVal = v;
            }
        }

        return maxVal;
    }
}
