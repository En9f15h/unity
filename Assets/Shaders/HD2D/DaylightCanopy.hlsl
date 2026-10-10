#ifndef HD2D_DAYLIGHT_CANOPY_INCLUDED
#define HD2D_DAYLIGHT_CANOPY_INCLUDED
// A world-space veil, shared by the 3D backdrop and original 2D maps.
half3 HD2DDaylightCanopy(float2 worldXY, float4 sunOrigin, float strength, half3 color, float4 shape, float4 detail)
{
    float drop=max(0,sunOrigin.y-worldXY.y);
    float offset=worldXY.x-(sunOrigin.x+drop*shape.x);
    float width=max(.2,shape.y+drop*shape.z);
    float shaft=exp(-pow(offset/width,2)*max(.5,shape.w));
    float sideShaft=exp(-pow((offset-3.2)/(width*.42),2)*2)*detail.x;
    float height=saturate((worldXY.y-sunOrigin.z)/max(1,sunOrigin.y-sunOrigin.z));
    float fade=smoothstep(0,.3,height)*(.3+.7*height);
    float crown=exp(-pow(offset/5,2))*pow(height,3)*detail.y;
    return color*strength*((shaft+sideShaft)*fade+crown);
}
#endif
