using UnityEngine;
public enum PCFMode {
	PCF2x2, PCF3x3, PCF5x5, PCF7x7,PCSS
}

[System.Serializable]
public class ShadowSettings {
	
	[Min(0.001f)]
	public float maxDistance = 100f;
	
	[Range(0.001f, 1f)]
	public float distanceFade = 0.1f;

    public enum TextureSize {
		_256 = 256, _512 = 512, _1024 = 1024,
		_2048 = 2048, _4096 = 4096, _8192 = 8192
	}
    [System.Serializable]
	public struct Directional {
		public Vector3 CascadeRatios =>
			new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);
		public TextureSize atlasSize;
		[Range(1, 4)]
		public int cascadeCount;

		[Range(0f, 1f)]
		public float cascadeRatio1, cascadeRatio2, cascadeRatio3;
		[Range(0.001f, 1f)]
		public float cascadeFade;
		public PCFMode filterMode;
		[Range(0.001f, 3f)]
		public float PCSS_FilterRadius;
		[Range(0.001f, 1f)]
		public float PCSS_SearchRadius;
		public enum CascadeBlendMode {
			Hard, Soft, Dither
		}

		public CascadeBlendMode cascadeBlend;
	}
	[System.Serializable]
	public struct Other {

		public TextureSize atlasSize;

		public PCFMode filter;
	}

	public Other other = new Other {
		atlasSize = TextureSize._1024,
		filter = PCFMode.PCF2x2
	};
	public Directional directional = new Directional {
		atlasSize = TextureSize._1024,
		cascadeCount = 4,
		cascadeRatio1 = 0.1f,
		cascadeRatio2 = 0.25f,
		cascadeRatio3 = 0.5f,
		cascadeFade = 0.1f,
		filterMode = PCFMode.PCF2x2,
		cascadeBlend = Directional.CascadeBlendMode.Hard,
		PCSS_FilterRadius = 0.1f,
		PCSS_SearchRadius = 0.75f
	};
}