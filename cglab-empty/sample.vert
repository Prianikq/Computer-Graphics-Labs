#version 120
// Вершинный шейдер

uniform float time; 
attribute vec3 coord;
attribute vec3 color;
varying vec3 fragColor;
varying vec2 fragCoord;


void main() {
    fragColor = color;
    fragCoord = coord.xy;
    gl_Position = vec4(coord * (1.0 - clamp(sin(time), 0.1, 0.6)),  1.0);
}
