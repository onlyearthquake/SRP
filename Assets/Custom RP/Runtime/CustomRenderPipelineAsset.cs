using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset {
	[SerializeField]
	PostFXSettings postFXSettings = default;
	[SerializeField]
	ShadowSettings shadows = default;

	[SerializeField]
	bool useDynamicBatching = true, useGPUInstancing = true,
		useSRPBatcher = true, useLightsPerObject = true;
	[SerializeField]
	Texture2D NoiseTex;

	protected override RenderPipeline CreatePipeline () {
		return new CustomRenderPipeline(
			NoiseTex,
			useDynamicBatching, useGPUInstancing, useSRPBatcher,
			useLightsPerObject, shadows, postFXSettings
		);
	}
}