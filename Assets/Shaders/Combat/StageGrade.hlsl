#ifndef COMBAT_STAGE_GRADE_INCLUDED
#define COMBAT_STAGE_GRADE_INCLUDED
// Operates on linear RGB in this project. Compression around middle grey retains dark detail.
half3 CombatStageGrade(half3 rgb, float4 grade)
{
    half luminance = dot(rgb, half3(.2126, .7152, .0722));
    rgb = lerp(luminance.xxx, rgb, grade.z);
    return max(0, (rgb - .18) * grade.y + .18) * grade.x;
}
#endif
