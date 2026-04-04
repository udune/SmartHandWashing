using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit 커스텀 VisualElement.
/// painter2D로 24시간 막대 그래프를 직접 드로우.
/// </summary>
public class BarChartElement : VisualElement
{
    private const int   HoursPerDay   = 24;
    private const float PaddingLeft   = 8f;
    private const float PaddingRight  = 8f;
    private const float PaddingBottom = 20f;
    private const float PaddingTop    = 8f;
    private const float BarGapRatio   = 0.15f;
    private const float AxisLineWidth = 0.5f;
    private const float MinBarHeight  = 1f;

    private static readonly Color BarNormal = new Color(0.26f, 0.60f, 0.26f, 1f);
    private static readonly Color BarPeak   = new Color(1.00f, 0.57f, 0.00f, 1f);
    private static readonly Color BarAxis   = new Color(0.47f, 0.56f, 0.61f, 0.5f);

    private int[] _data     = new int[HoursPerDay];
    private int   _peakHour = -1;

    public BarChartElement()
    {
        generateVisualContent += OnGenerateVisualContent;
    }

    public void SetData(int[] hourlyCount, int peakHour)
    {
        _data     = hourlyCount;
        _peakHour = peakHour;
        MarkDirtyRepaint();
    }

    private void OnGenerateVisualContent(MeshGenerationContext ctx)
    {
        var painter = ctx.painter2D;
        float w     = contentRect.width;
        float h     = contentRect.height;

        if (w <= 0 || h <= 0)
        {
            return;
        }

        float chartW = w - PaddingLeft - PaddingRight;
        float chartH = h - PaddingBottom - PaddingTop;

        int maxVal = GetMaxValue();

        float barW   = chartW / HoursPerDay;
        float barGap = barW * BarGapRatio;
        float bottom = h - PaddingBottom;

        // X축 기준선
        painter.strokeColor = BarAxis;
        painter.lineWidth   = AxisLineWidth;
        painter.BeginPath();
        painter.MoveTo(new Vector2(PaddingLeft, bottom));
        painter.LineTo(new Vector2(w - PaddingRight, bottom));
        painter.Stroke();

        // 막대 드로잉
        for (int hour = 0; hour < HoursPerDay; hour++)
        {
            float barHeight = _data[hour] > 0
                ? (float)_data[hour] / maxVal * chartH
                : MinBarHeight;

            float x = PaddingLeft + hour * barW + barGap * 0.5f;
            float y = bottom - barHeight;

            painter.fillColor = (hour == _peakHour && _data[hour] > 0)
                ? BarPeak
                : BarNormal;

            painter.BeginPath();
            painter.MoveTo(new Vector2(x,                 bottom));
            painter.LineTo(new Vector2(x,                 y));
            painter.LineTo(new Vector2(x + barW - barGap, y));
            painter.LineTo(new Vector2(x + barW - barGap, bottom));
            painter.ClosePath();
            painter.Fill();
        }
    }

    private int GetMaxValue()
    {
        int maxVal = 1;

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
