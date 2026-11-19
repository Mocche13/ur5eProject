using UnityEngine;

/// <summary>
/// Profilo di velocità trapezoidale (o triangolare se la distanza è corta):
/// accelerazione costante -> velocità di crociera -> decelerazione costante.
/// È lo stesso tipo di profilo usato dai controller dei robot industriali.
/// Funziona con qualsiasi unità (gradi, metri...) purché coerente.
/// </summary>
public struct TrapezoidalProfile
{
    public readonly float Distance;
    public readonly float Duration;

    readonly float acceleration;
    readonly float peakVelocity;
    readonly float tAccel;
    readonly float tCruise;

    public TrapezoidalProfile(float distance, float maxVelocity, float acceleration)
    {
        float d = Mathf.Max(0f, distance);
        float v = Mathf.Max(1e-6f, maxVelocity);
        float a = Mathf.Max(1e-6f, acceleration);

        Distance = d;
        this.acceleration = a;

        if (d <= 1e-9f)
        {
            peakVelocity = 0f;
            tAccel = 0f;
            tCruise = 0f;
            Duration = 0f;
        }
        else
        {
            float distAccelPlusDecel = v * v / a;
            if (d >= distAccelPlusDecel)
            {
                // trapezio: raggiunge la velocità massima
                peakVelocity = v;
                tAccel = v / a;
                tCruise = (d - distAccelPlusDecel) / v;
            }
            else
            {
                // triangolo: non fa in tempo a raggiungere la velocità massima
                peakVelocity = Mathf.Sqrt(d * a);
                tAccel = peakVelocity / a;
                tCruise = 0f;
            }
            Duration = 2f * tAccel + tCruise;
        }
    }

    /// <summary>Distanza percorsa al tempo t (0..Distance).</summary>
    public float Evaluate(float t)
    {
        if (Distance <= 0f || t <= 0f) return 0f;
        if (t >= Duration) return Distance;

        if (t < tAccel)
            return 0.5f * acceleration * t * t;

        if (t < tAccel + tCruise)
            return 0.5f * acceleration * tAccel * tAccel + peakVelocity * (t - tAccel);

        float td = Duration - t;
        return Distance - 0.5f * acceleration * td * td;
    }

    /// <summary>Avanzamento normalizzato al tempo t (0..1).</summary>
    public float EvaluateNormalized(float t)
    {
        return Distance > 0f ? Evaluate(t) / Distance : 1f;
    }
}
