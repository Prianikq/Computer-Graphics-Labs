#version 150 core

attribute vec4 Normal;
attribute vec4 Coord;

varying vec3 FragNormale;
varying vec3 FragVertex;

uniform mat4 Projection;
uniform mat4 ModelView;
uniform mat4 NormalMatrix;

void main(void)
{
    FragVertex = vec3(ModelView * Coord);
    FragNormale = vec3(NormalMatrix * Normal);

    gl_Position = Projection * ModelView * Coord;
    
}