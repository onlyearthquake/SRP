#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

float3 IncomingLight (Surface surface, Light light) {
	return saturate(dot(surface.normal, light.direction)) * light.color * light.attenuation;
}
float3 GetLighting (Surface surface, Light light,BRDF brdf) {
	return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

float3 GetLighting (Surface surface, BRDF brdf,GI gi) {
	ShadowData shadowData = GetShadowData(surface);
	shadowData.shadowMask = gi.shadowMask;
	
	float3 color = IndirectBRDF(surface, brdf, gi.diffuse, gi.specular);
	//Directional Lights
	for (int i = 0; i < GetDirectionalLightCount(); i++) {
		Light light = GetDirectionalLight(i, surface, shadowData);
		color += GetLighting(surface,light,brdf);
	}
	//Other Lights
	#if defined(_LIGHTS_PER_OBJECT)
		for (int j = 0;j < min(unity_LightData.y, 8); j++) {
			int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
			Light light = GetOtherLight(lightIndex, surface, shadowData);
			color += GetLighting(surface, light, brdf);
		}
	#else
		for (int j = 0; j < GetOtherLightCount(); j++) {
			Light light = GetOtherLight(j, surface, shadowData);
			color += GetLighting(surface, light, brdf);
		}
	#endif
	return color;
}

#endif