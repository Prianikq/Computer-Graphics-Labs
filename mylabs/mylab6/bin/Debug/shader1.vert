#version 120
// Вершинный шейдер

varying vec3 FragNormale;
varying vec3 FragVertex;
varying vec3 FragColour;

void main(void)
{
    FragVertex = vec3(gl_ModelViewMatrix * gl_Vertex);
    FragNormale = normalize(gl_NormalMatrix * gl_Normal);
    FragColour = vec3(gl_Color); // ???
    gl_Position = ftransform();
}