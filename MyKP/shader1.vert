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

void main(void)
{
    FragVertex = vec3(PointMatrix * Coord);
    FragNormale = vec3(NormalMatrix * Normal);
    FragNormale = normalize(FragNormale);
    gl_Position = (Projection * ModelView) * Coord;
}