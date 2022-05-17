#ifndef CUSTOM_RANDOM_INCLUDED
#define CUSTOM_RANDOM_INCLUDED
float2 VogelDiskSampleDir(int sampleIndex, int samplesCount, float phi)
{
	float GoldenAngle = 2.4f;

	//float r = sqrt(sampleIndex + 0.5f) / sqrt(samplesCount);
	float r = sqrt(sampleIndex + 0.5f) / sqrt(samplesCount);
	float theta = sampleIndex * GoldenAngle + phi;

	float sine, cosine;
	sincos(theta, sine, cosine);
	
	return float2(r * cosine, r * sine);
}
uint RandomSeed(float2 uv, float2 screenWH)
{
    return uint(
        uint(uv.x * screenWH.x)  * uint(1973) + 
        uint(uv.y * screenWH.y) * uint(9277) + 
        uint(114514) * uint(26699)) | uint(1);
}

uint wang_hash(inout uint seed) {
    seed = uint(seed ^ uint(61)) ^ uint(seed >> uint(16));
    seed *= uint(9);
    seed = seed ^ (seed >> 4);
    seed *= uint(0x27d4eb2d);
    seed = seed ^ (seed >> 15);
    return seed;
}
 
float rand(inout uint seed) {
    return float(wang_hash(seed)) / 4294967296.0;
}
#endif