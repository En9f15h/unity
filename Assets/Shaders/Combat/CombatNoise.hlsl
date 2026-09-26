#ifndef COMBAT_NOISE_INCLUDED
#define COMBAT_NOISE_INCLUDED
// Shared unscaled presentation clock, independent of combat/physics state.
float _CombatVisualTime;
float CombatHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}
float CombatNoise(float2 p)
{
    float2 cell = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(CombatHash(cell), CombatHash(cell + float2(1,0)), f.x),
                lerp(CombatHash(cell + float2(0,1)), CombatHash(cell + 1), f.x), f.y);
}
#endif
