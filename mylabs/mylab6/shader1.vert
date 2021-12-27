#version 150 core

attribute vec4 Normal;
attribute vec4 Coord;

out vec3 FragNormale;
out vec3 FragVertex;

uniform mat4 Projection;
uniform mat4 ModelView;
uniform mat4 NormalMatrix;
uniform mat4 ModelMatrix;
uniform mat4 PointMatrix;

uniform float Time; 

void main(void)
{
    vec4 Coord_now = Coord;
    if (Time > 0)
        Coord_now.y = Coord.y * cos(Time + Coord.y);
    FragVertex = vec3(PointMatrix * Coord_now);
    FragNormale = vec3(NormalMatrix * Normal);
    FragNormale = normalize(FragNormale);
    gl_Position = (Projection * ModelView) * Coord_now;
}