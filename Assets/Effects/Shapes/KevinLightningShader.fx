sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
sampler uImage2 : register(s2);
float3 uColor;
float3 uSecondaryColor;
float uOpacity;
float uSaturation;
float uRotation;
float uTime;
float4 uSourceRect;
float2 uWorldPosition;
float uDirection;
float3 uLightSource;
float2 uImageSize0;
float2 uImageSize1;
float2 uImageSize2;
float2 actualSize;
float4 uShaderSpecificData;
float2 screenMoveOffset;
float2 lightningDirection;
float lightningAngle;
float2 noiseCoordsOffset;
float currentFrame;
float lightningLength;
bool bigArc;

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(uImage0, coords);
    float luminosity = (color.r + color.g + color.b) / 3;
    return pow(color, 4) * sampleColor * luminosity;
}

float2 RotatedBy(float2 v, float angle)
{
    float c = sin(angle + 1.5707);
    float s = sin(angle);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

float FractalNoise(float2 coords)
{
    float result = 0;
    float gain = 2;
    float amplitude = 0.5;
    for (int i = 0; i < 5; i++)
    {
        result += tex2D(uImage1, coords).r * amplitude;
        coords *= gain;
        amplitude *= 0.5;
    }
    return result * 2 - 1;
}

float4 UpdatePreviousState(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float2 rotatedCoords = RotatedBy(coords - 0.5, lightningAngle) + 0.5;
    float4 color = tex2D(uImage0, rotatedCoords);
    float4 result = color;
    
    // Make the color exponentionally decay towards a cyan.
    result *= float4(0.81 + uColor.r * 0.14, 0.81 + uColor.g * 0.14, 0.81 + uColor.b * 0.14, 1) * 0.88;
    
    // Apply the lightning effects. If necessary, this will apply random jumps in the noise to create slightly large, more varied arcs.
    float2 baseNoiseCoords = (coords + noiseCoordsOffset) * 0.9;
    float2 noiseCoords = float2(baseNoiseCoords.x, currentFrame * floor(1 + abs(coords.y) * 3) * 0.02);
    float noise = FractalNoise(noiseCoords) * 1.1;
    if (bigArc)
        noise *= 1.5;
    
    // Calculate the lighting.
    float zoomFactor = 12;
    float2 direction = normalize(coords - 0.5);    
    float4 brightness = 0.0156 / abs(coords.y * zoomFactor - noise - zoomFactor * 0.5);
    result += brightness * direction.x * smoothstep(0.04, 0.12, coords.x) * smoothstep(lightningLength, lightningLength - 0.03, coords.x);
    
    return result;
}

technique Technique1
{
    pass BurnPass
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
    pass UpdatePass
    {
        PixelShader = compile ps_2_0 UpdatePreviousState();
    }
}