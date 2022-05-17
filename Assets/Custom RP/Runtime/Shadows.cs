using UnityEngine;
using UnityEngine.Rendering;

public class Shadows {

	const string bufferName = "Shadows";

	CommandBuffer buffer = new CommandBuffer {
		name = bufferName
	};

	ScriptableRenderContext context;

	CullingResults cullingResults;

	ShadowSettings settings;
#region Const
    const int maxShadowedDirectionalLightCount = 4, 
		maxShadowedOtherLightCount = 16,
		maxCascades = 4;
	static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
		dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
		cascadeCountId = Shader.PropertyToID("_CascadeCount"),
		cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
		cascadeDataId = Shader.PropertyToID("_CascadeData"),
		shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
		shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"),
		shadowPancakingId = Shader.PropertyToID("_ShadowPancaking"),
		otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"),
		otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"),
		otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles");
	static string[] directionalFilterKeywords = {
		"_DIRECTIONAL_PCF3",
		"_DIRECTIONAL_PCF5",
		"_DIRECTIONAL_PCF7",
		"_DIRECTIONAL_PCSS"
	};
	static string[] otherFilterKeywords = {
		"_OTHER_PCF3",
		"_OTHER_PCF5",
		"_OTHER_PCF7",
	};
	static string[] cascadeBlendKeywords = {
		"_CASCADE_BLEND_SOFT",
		"_CASCADE_BLEND_DITHER"
	};
	static string[] shadowMaskKeywords = {
		"_SHADOW_MASK_ALWAYS",
		"_SHADOW_MASK_DISTANCE"
	};
#endregion
    struct ShadowedDirectionalLight {
		public int visibleLightIndex;
		public float slopeScaleBias;
		public float nearPlaneOffset;
	}
	struct ShadowedOtherLight {
		public int visibleLightIndex;
		public float slopeScaleBias;
		public float normalBias;
		public bool isPoint;
	}

	ShadowedDirectionalLight[] ShadowedDirectionalLights = 
		new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
		
	static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades],
		otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];
	int shadowedDirLightCount, shadowedOtherLightCount;
	bool useShadowMask;
    public void Setup (
		ScriptableRenderContext context, CullingResults cullingResults,
		ShadowSettings settings
	) {
		this.context = context;
		this.cullingResults = cullingResults;
		this.settings = settings;
        shadowedDirLightCount = shadowedOtherLightCount = 0;
		useShadowMask = false;
	}
	Vector4 atlasSizes;
	public void Render(){
		if (shadowedDirLightCount > 0) {
			RenderDirectionalShadows();
		}else {
			//Create Minnn Texture For WebGL 2.0
			buffer.GetTemporaryRT(
				dirShadowAtlasId, 1, 1,
				32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
			);
		}
		if (shadowedOtherLightCount > 0) {
			RenderOtherShadows();
		}
		else {
			buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
		}
		buffer.BeginSample(bufferName);
		SetKeywords(shadowMaskKeywords,useShadowMask ?
			QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 :
			-1);

		buffer.SetGlobalInt(cascadeCountId, shadowedDirLightCount > 0 ? 
			settings.directional.cascadeCount : 0);
		//Squre Formula
		float f = 1f - settings.directional.cascadeFade;
		buffer.SetGlobalVector(
			shadowDistanceFadeId, new Vector4(
				1f / settings.maxDistance, 1f / settings.distanceFade,
				1f / (1f - f * f)
			)
		);
		buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);
		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}
	static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades],
		cascadeData = new Vector4[maxCascades],
		otherShadowTiles = new Vector4[maxShadowedOtherLightCount];

	void RenderOtherShadows () {
		int atlasSize = (int)settings.other.atlasSize;
		atlasSizes.z = atlasSize;
		atlasSizes.w = 1f / atlasSize;
		//Create Render Target
		buffer.GetTemporaryRT(otherShadowAtlasId, atlasSize, atlasSize,
			32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		buffer.SetRenderTarget(
			otherShadowAtlasId,
			RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
		);
		buffer.ClearRenderTarget(true, false, Color.clear);
		buffer.SetGlobalFloat(shadowPancakingId, 0f);
		buffer.BeginSample(bufferName);
		ExecuteBuffer();
		//Divide Whole Alta Into Pieces
		int tiles = shadowedOtherLightCount;

		int split = (tiles <= 1 ? 1 : (tiles <= 4 ? 2 : 4));
		int tileSize = atlasSize / split;
		for (int i = 0; i < shadowedOtherLightCount;) {
			//If Point,Use CubeMap,Therefore Add 6 for index
			if (shadowedOtherLights[i].isPoint) {
				RenderPointShadows(i, split, tileSize);
				i += 6;
			}
			else {
				RenderSpotShadows(i, split, tileSize);
				i += 1;
			}
		}
		buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
		buffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);
		SetKeywords(otherFilterKeywords, (int)settings.other.filter - 1);

		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}
	void RenderDirectionalShadows () {
		int atlasSize = (int)settings.directional.atlasSize;
		atlasSizes.x = atlasSize;
		atlasSizes.y = 1f / atlasSize;
		//Create Render Target
		buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize,
			32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		buffer.SetRenderTarget(
			dirShadowAtlasId,
			RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
		);
		//Use Unity Panking Optimization
		buffer.SetGlobalFloat(shadowPancakingId, 1f);
		buffer.ClearRenderTarget(true, false, Color.clear);
		
		buffer.BeginSample(bufferName);
		ExecuteBuffer();
		//Divide Whole Alta Into Pieces
		int tiles = shadowedDirLightCount * settings.directional.cascadeCount;

		int split = (tiles <= 1 ? 1 : (tiles <= 4 ? 2 : 4));
		
		int tileSize = atlasSize / split;

		for (int i = 0; i < shadowedDirLightCount; i++) {
			RenderDirectionalShadows(i, split, tileSize);
		}
		buffer.SetGlobalFloat("_PCSS_SearchRadius",settings.directional.PCSS_SearchRadius);
		buffer.SetGlobalFloat("_PCSS_FilterRadius",settings.directional.PCSS_FilterRadius);
		buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
		buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
		buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
		
		SetKeywords(
			directionalFilterKeywords, (int)settings.directional.filterMode - 1
		);
		SetKeywords(
			cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1
		);
		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}
	ShadowedOtherLight[] shadowedOtherLights =
		new ShadowedOtherLight[maxShadowedOtherLightCount];
	void RenderDirectionalShadows (int index, int split, int tileSize) {
		ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
		var shadowSettings =
			new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
		
		int cascadeCount = settings.directional.cascadeCount;
		int tileOffset = index * cascadeCount;
		Vector3 ratios = settings.directional.CascadeRatios;

		float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);
		for (int i = 0; i < cascadeCount; i++) {
			cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
				light.visibleLightIndex, i,
				cascadeCount, ratios, tileSize, 
				light.nearPlaneOffset,
				out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
				out ShadowSplitData splitData
			);
			splitData.shadowCascadeBlendCullingFactor = cullingFactor;
			shadowSettings.splitData = splitData;
			
			if (index == 0) {
				//Just Squre W ,You Can do this in Shader , too
				Vector4 cullingSphere = splitData.cullingSphere;
				float texelSize = 2f * cullingSphere.w / tileSize;
				//For Normal Bias
				float filterSize = texelSize * ((float)settings.directional.filterMode + 1f);
				//Decrease The Radius Of CullingSphere
				//For PCF Sampling
				cullingSphere.w -= filterSize;
				cullingSphere.w *= cullingSphere.w;
				cascadeCullingSpheres[i] = cullingSphere;
				
				cascadeData[i] = new Vector4(
					1f / cullingSphere.w,
					filterSize * 1.4142136f);
			}
			//Not Command In SRP (If You Use Custome MVP Matrix Varible Name)
			//Here We Use Standard Func , Maybe is OK
			int tileIndex = tileOffset + i;
			float tileScale = 1f / split;
			//VP Maxtrix Modify
			dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
				projectionMatrix * viewMatrix,
				SetTileViewport(tileIndex, split, tileSize),tileScale
			);
			buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
			buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
			ExecuteBuffer();
			context.DrawShadows(ref shadowSettings);
			buffer.SetGlobalDepthBias(0f, 0f);
		}
	}
	void RenderSpotShadows (int index, int split, int tileSize) {
		ShadowedOtherLight light = shadowedOtherLights[index];
		var shadowSettings =
			new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
		cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
			light.visibleLightIndex, out Matrix4x4 viewMatrix,
			out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
		);
		shadowSettings.splitData = splitData;
		float texelSize = 2f / (tileSize * projectionMatrix.m00);
		float filterSize = texelSize * ((float)settings.other.filter + 1f);
		float bias = light.normalBias * filterSize * 1.4142136f;
		Vector2 offset = SetTileViewport(index, split, tileSize);
		float tileScale = 1f / split;
		SetOtherTileData(index, offset, tileScale,bias);
		otherShadowMatrices[index] = ConvertToAtlasMatrix(
			projectionMatrix * viewMatrix, offset, tileScale
		);
		buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
		buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
		ExecuteBuffer();
		context.DrawShadows(ref shadowSettings);
		buffer.SetGlobalDepthBias(0f, 0f);
	}
	void RenderPointShadows (int index, int split, int tileSize) {
		ShadowedOtherLight light = shadowedOtherLights[index];
		var shadowSettings =
			new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
		float texelSize = 2f / tileSize;
		float filterSize = texelSize * ((float)settings.other.filter + 1f);
		float bias = light.normalBias * filterSize * 1.4142136f;
		float tileScale = 1f / split;
		for (int i = 0; i < 6; i++) {
			float fovBias =
				Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
			cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
				light.visibleLightIndex, (CubemapFace)i, fovBias,
				out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
				out ShadowSplitData splitData
			);
			shadowSettings.splitData = splitData;
			int tileIndex = index + i;
			Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
			SetOtherTileData(tileIndex, offset, tileScale, bias);
			otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
				projectionMatrix * viewMatrix, offset, tileScale
			);

			buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
			buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
			ExecuteBuffer();
			context.DrawShadows(ref shadowSettings);
			buffer.SetGlobalDepthBias(0f, 0f);
		}
	}
	
#region ReserveShadow
    public Vector4  ReserveDirectionalShadows (Light light, int visibleLightIndex) {
        //Not Full && Light Generate Shadow && Affect Scene
		if (shadowedDirLightCount < maxShadowedDirectionalLightCount &&
			light.shadows != LightShadows.None && light.shadowStrength > 0f) {
				//If Enable ShadowMask In Bake Setting
				LightBakingOutput lightBaking = light.bakingOutput;
				float maskChannel = -1;
				if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed && 
					lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask) {
					useShadowMask = true;
					maskChannel = lightBaking.occlusionMaskChannel;
				}
				//Delay Judge For: Don't Have A RealTime ShadowCaster,But Will Bake
				if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)) {
					return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
				}
				ShadowedDirectionalLights[shadowedDirLightCount] =
				new ShadowedDirectionalLight {
					visibleLightIndex = visibleLightIndex,
					slopeScaleBias = light.shadowBias,
					nearPlaneOffset = light.shadowNearPlane
				};
				//return strength && index
				return new Vector4(
					light.shadowStrength,
					settings.directional.cascadeCount * shadowedDirLightCount++,
					light.shadowNormalBias, maskChannel
				);
		}
		return new Vector4(0f, 0f, 0f, -1f);
	}
	public Vector4 ReserveOtherShadows (Light light, int visibleLightIndex) {
		if(light.shadows == LightShadows.None ||
			light.shadowStrength <= 0f){
			return new Vector4(0f, 0f, 0f, -1f);
		}
		LightBakingOutput lightBaking = light.bakingOutput;
		float maskChannel = -1;
		if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
			lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
		) {
			useShadowMask = true;
			maskChannel = lightBaking.occlusionMaskChannel;
		}
		bool isPoint = light.type == LightType.Point;
		//Consider MaxLightCount As MaxTileCount
		//Point -> CubeMap,Take Up 6 Tiles
		int newLightCount = shadowedOtherLightCount + (isPoint ? 6 : 1);
		if (newLightCount >= maxShadowedOtherLightCount ||
			!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)
		) {
			//OnlyBake
			return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
		}
		shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight {
			visibleLightIndex = visibleLightIndex,
			slopeScaleBias = light.shadowBias,
			normalBias = light.shadowNormalBias,
			isPoint = isPoint
		};
		//Mixed
		Vector4 data = new Vector4(
			light.shadowStrength, shadowedOtherLightCount,
			isPoint ? 1f : 0f, maskChannel
		);
		shadowedOtherLightCount = newLightCount;
		return data;
	}
#endregion
#region HelpFunc
	void ExecuteBuffer () {
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
	public void CleanUp () {
		buffer.ReleaseTemporaryRT(dirShadowAtlasId);
		if (shadowedOtherLightCount > 0) {
			buffer.ReleaseTemporaryRT(otherShadowAtlasId);
		}
		ExecuteBuffer();
	}
	Matrix4x4 ConvertToAtlasMatrix (Matrix4x4 m, Vector2 offset,float scale) {
		if (SystemInfo.usesReversedZBuffer) {
			m.m20 = -m.m20;
			m.m21 = -m.m21;
			m.m22 = -m.m22;
			m.m23 = -m.m23;
		}
		//Convert -1 ~ 1 into 0 ~ 1
		m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
		m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
		m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
		m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
		m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
		m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
		m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
		m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
		m.m20 = 0.5f * (m.m20 + m.m30);
		m.m21 = 0.5f * (m.m21 + m.m31);
		m.m22 = 0.5f * (m.m22 + m.m32);
		m.m23 = 0.5f * (m.m23 + m.m33);
		return m;
	}
	Vector2 SetTileViewport (int index, int split, float tileSize) {
		Vector2 offset = new Vector2(index % split, index / split);
		buffer.SetViewport(new Rect(
			offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
		));
		return offset;
	}
	void SetOtherTileData (int index, Vector2 offset, float scale, float bias) {
		float border = atlasSizes.w * 0.5f;
		Vector4 data = new Vector4(0,0,0,0);
		data.x = offset.x * scale + border;
		data.y = offset.y * scale + border;
		data.z = scale - border - border;
		data.w = bias;
		otherShadowTiles[index] = data;
		return;
	}
	void SetKeywords (string[] keywords, int enabledIndex) {
		for (int i = 0; i < keywords.Length; i++) {
			if (i == enabledIndex) {
				buffer.EnableShaderKeyword(keywords[i]);
			}
			else {
				buffer.DisableShaderKeyword(keywords[i]);
			}
		}
	}
#endregion
}