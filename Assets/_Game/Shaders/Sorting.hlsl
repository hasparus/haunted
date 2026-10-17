// Who covers whom (plans/sorting.md). Scenery (floor, walls, belt, tube) is drawn as if it stood SORT_SCENERY_PUSH
// metres further from the camera than it does, so it covers only the floor and never anything standing in the room.
// Everything else is drawn at the depth of a point it stands on, so height never brings part of it in front of
// something standing in front of it.
#ifndef HAUNTED_SORTING
#define HAUNTED_SORTING

#define SORT_SCENERY_PUSH 4.0

// Clip-space z for a vertex at positionCS, drawn at the depth of the world point `at`.
float SortedZ(float4 positionCS, float3 at)
{
    float4 cs = TransformWorldToHClip(at);
    return cs.z / cs.w * positionCS.w;
}

float3 PushedBack(float3 positionWS, float metres) { return positionWS + GetViewForwardDir() * metres; }

float3 Underneath(float3 positionWS) { return float3(positionWS.x, 0, positionWS.z); }

#endif
