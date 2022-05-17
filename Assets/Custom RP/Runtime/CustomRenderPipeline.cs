using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline {
	// Per Camera Renderer
	CameraRenderer renderer = new CameraRenderer();
	Texture2D noiseTex;
	bool useDynamicBatching, useGPUInstancing, useLightsPerObject;
	ShadowSettings shadowSettings;
	PostFXSettings postFXSettings;
	public CustomRenderPipeline (
		Texture2D noiseTex,
		bool useDynamicBatching, bool useGPUInstancing,
		bool useSRPBatcher, bool useLightsPerObject,
		ShadowSettings shadowSettings,PostFXSettings postFXSettings
	) {
		this.noiseTex = noiseTex;
		this.useDynamicBatching = useDynamicBatching;
		this.useGPUInstancing = useGPUInstancing;
		this.useLightsPerObject = useLightsPerObject;
		this.shadowSettings = shadowSettings;
		this.postFXSettings = postFXSettings;
		GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
		GraphicsSettings.lightsUseLinearIntensity = true;
		InitializeForEditor();
	}

	protected override void Render (
		ScriptableRenderContext context, Camera[] cameras
	) {
		Shader.SetGlobalTexture("_noiseTex",noiseTex);
		foreach (Camera camera in cameras) {
			renderer.Render(
				context, camera,
				useDynamicBatching, useGPUInstancing, 
				useLightsPerObject,
				shadowSettings, postFXSettings
			);
		}
	}
}