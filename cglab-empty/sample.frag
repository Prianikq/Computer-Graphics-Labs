#version 120
// Фрагментный шейдер

uniform float time;
varying vec3 fragColor;
varying vec2 fragCoord;
const float phi = 1.6180339887498948482;

float noise(in vec2 xy, in float seed) {
    return fract(tan(distance(xy * phi, xy) * seed) * xy.x);
}

void main() {
    vec2 _coord = vec2(abs(fragCoord.x), fragCoord.y);
    gl_FragColor = vec4(fragColor * noise(_coord, 10.0 * cos(time / 3.0)), 1.0);
}
