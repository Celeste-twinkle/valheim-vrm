using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ValheimVRM
{
	public class MToonColorSync : MonoBehaviour
	{
		class MatColor
		{
			public Material mat;
			public Color color;
			public Color shadeColor;
			public Color emission;
			public bool hasColor;
			public bool hasShadeColor;
			public bool hasEmission;
		}

		//private int _SunFogColor;
		private int _SunColor;
		private int _AmbientColor;

		private readonly List<Material> ownedMaterials = new List<Material>();
		private List<MatColor> matColors = new List<MatColor>();

		void Awake()
		{
			//_SunFogColor = Shader.PropertyToID("_SunFogColor");
			_SunColor = Shader.PropertyToID("_SunColor");
			_AmbientColor = Shader.PropertyToID("_AmbientColor");
		}

		public void Setup(GameObject vrm)
		{
			matColors.Clear();
			foreach (var smr in vrm.GetComponentsInChildren<SkinnedMeshRenderer>())
			{
				// Both MToon generations and Standard already use scene lights.
				// Never multiply their albedo/emission by the global sun a second time.
				// Inspect
				// sharedMaterials first: .materials would allocate even when skipped.
				var materials = smr.sharedMaterials;
				for (int i = 0; i < materials.Length; i++)
				{
					var mat = materials[i];
					// Unsupported legacy unlit/game shaders retain their old driver.
					if (mat == null || mat.shader == null || mat.shader.name == "Standard" || AvatarRenderingTarget.Supports(mat)) continue;
					if (!ownedMaterials.Contains(mat))
					{
						mat = new Material(mat);
						ownedMaterials.Add(mat);
						materials[i] = mat;
					}
					if (!matColors.Exists(m => m.mat == mat))
					{
						matColors.Add(new MatColor()
						{
							mat = mat,
							color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white,
							shadeColor = mat.HasProperty("_ShadeColor") ? mat.GetColor("_ShadeColor") : Color.white,
							emission = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black,
							hasColor = mat.HasProperty("_Color"),
							hasShadeColor = mat.HasProperty("_ShadeColor"),
							hasEmission = mat.HasProperty("_EmissionColor"),
						});
					}
				}
				smr.sharedMaterials = materials;
			}
		}

		void OnDestroy()
		{
			foreach (var mat in ownedMaterials) if (mat != null) Destroy(mat);
			ownedMaterials.Clear();
		}

		void Update()
		{
			//var fog = Shader.GetGlobalColor(_SunFogColor);
			var sun = Shader.GetGlobalColor(_SunColor);
			var amb = Shader.GetGlobalColor(_AmbientColor);
			var sunAmb = sun + amb;

			foreach (var matColor in matColors)
			{
				var col = matColor.color * sunAmb;
				col.a = matColor.color.a;

				var shadeCol = matColor.shadeColor * sunAmb;
				shadeCol.a = matColor.shadeColor.a;

				var emi = matColor.emission * sunAmb.grayscale;

				if (matColor.hasColor) matColor.mat.SetColor("_Color", col);
				if (matColor.hasShadeColor) matColor.mat.SetColor("_ShadeColor", shadeCol);
				if (matColor.hasEmission) matColor.mat.SetColor("_EmissionColor", emi);
			}
		}
	}
}
