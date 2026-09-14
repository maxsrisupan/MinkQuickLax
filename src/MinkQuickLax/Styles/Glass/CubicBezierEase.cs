using System.Windows;
using System.Windows.Media.Animation;

namespace MinkQuickLax.Styles.Glass;

/// <summary>CSS-style cubic-bezier easing for WPF animations.</summary>
public sealed class CubicBezierEase : EasingFunctionBase
{
    private readonly double _x1;
    private readonly double _y1;
    private readonly double _x2;
    private readonly double _y2;

    public CubicBezierEase(double x1, double y1, double x2, double y2)
    {
        _x1 = x1;
        _y1 = y1;
        _x2 = x2;
        _y2 = y2;
        // EaseInCore is used as-is. Not frozen here: WPF clones easings through CreateInstanceCore and then copies properties.
        EasingMode = EasingMode.EaseIn;
    }

    public static CubicBezierEase Frozen(double x1, double y1, double x2, double y2)
    {
        var ease = new CubicBezierEase(x1, y1, x2, y2);
        ease.Freeze();
        return ease;
    }

    protected override double EaseInCore(double normalizedTime)
    {
        var t = SolveForT(normalizedTime);
        return Bezier(t, _y1, _y2);
    }

    protected override Freezable CreateInstanceCore() => new CubicBezierEase(_x1, _y1, _x2, _y2);

    private static double Bezier(double t, double p1, double p2)
    {
        var u = 1 - t;
        return 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t;
    }

    private double SolveForT(double x)
    {
        // Newton-Raphson, then bisection if the slope is too flat.
        var t = x;
        for (var i = 0; i < 8; i++)
        {
            var error = Bezier(t, _x1, _x2) - x;
            if (Math.Abs(error) < 1e-5)
            {
                return t;
            }
            var u = 1 - t;
            var slope = 3 * u * u * _x1 + 6 * u * t * (_x2 - _x1) + 3 * t * t * (1 - _x2);
            if (Math.Abs(slope) < 1e-6)
            {
                break;
            }
            t -= error / slope;
        }
        double lo = 0;
        double hi = 1;
        t = x;
        for (var i = 0; i < 30; i++)
        {
            var value = Bezier(t, _x1, _x2);
            if (Math.Abs(value - x) < 1e-5)
            {
                break;
            }
            if (value < x)
            {
                lo = t;
            }
            else
            {
                hi = t;
            }
            t = (lo + hi) / 2;
        }
        return t;
    }
}
