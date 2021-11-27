#version 120
// Фрагментный шейдер

// Данные освещения
uniform vec3 Ka_Material;
uniform vec3 Kd_Material;
uniform vec3 Ks_Material;
uniform float P_Material;
uniform vec3 Ia_Material;
uniform vec3 Il_Material;
uniform vec3 LightPos; // ??
uniform vec2 Parameters;
uniform vec3 CameraPos; // ??

varying vec4 FragNormale;
varying vec3 FragVertex;
varying vec3 FragColour;



void main(void)
{
    vec4 L = vec4(LightPos.x - FragVertex.x, LightPos.y - FragVertex.y, LightPos.z - FragVertex.z, 0); // LIGHT IN WORLD SPACE
    float dist = length(L);
    L = normalize(L);

    /* Фоновая составляющая */
    float I_red = Ia_Material.x * Ka_Material.x;
    float I_green = Ia_Material.y * Ka_Material.y;
    float I_blue = Ia_Material.z * Ka_Material.z;

    /* Рассеянная составляющая */
    I_red += clamp(0, 1, Il_Material.x * Kd_Material.x * dot(L, FragNormale) / (Parameters[0] * dist + Parameters[1]));
    I_green += clamp(0, 1, Il_Material.y * Kd_Material.y * dot(L, FragNormale) / (Parameters[0] * dist + Parameters[1]));
    I_blue += clamp(0, 1, Il_Material.z * Kd_Material.z * dot(L, FragNormale) / (Parameters[0] * dist + Parameters[1]));

    /* Зеркальная составляющая */
    if (dot(L, FragNormale) > 0) 
    {
        vec4 S = vec4(CameraPos.x - FragVertex.x, CameraPos.y - FragVertex.y, CameraPos.z - FragVertex.z, 0); // -1000 - FragVertex.z ???
        vec4 R = vec4(reflect(-L, FragNormale)); // vec3 ???
        
        S = normalize(S);
        R = normalize(R);

        if (dot(R, S) > 0)
        {
            I_red += clamp(0, 1, Il_Material.x * Ks_Material.x * pow(dot(R, S), P_Material) / (Parameters[0] * dist + Parameters[1]));
            I_green += clamp(0, 1, Il_Material.y * Ks_Material.y * pow(dot(R, S), P_Material) / (Parameters[0] * dist + Parameters[1]));
            I_blue += clamp(0, 1, Il_Material.z * Ks_Material.z * pow(dot(R, S), P_Material) / (Parameters[0] * dist + Parameters[1]));
        }
    }

    I_red = min(1, I_red);
    I_green = min(1, I_green);
    I_blue = min(1, I_blue);

    vec4 result = vec4(I_red, I_green, I_blue, 0);

    gl_FragColor = gl_FrontLightModelProduct.sceneColor + result;
}