using System;
using Duality.Drawing;
using Duality.Resources;

namespace OgvPlayer
{
	[Serializable]
	public class OgvDrawTechnique : DrawTechnique
	{
		public Texture TextureOne { get; set; }
		public Texture TextureTwo { get; set; }
		public Texture TextureThree { get; set; }

		public override bool NeedsPreparation
		{
			get { return true; }
		}

		protected override void PrepareRendering(IDrawDevice device, BatchInfo material)
		{
			base.PrepareRendering(device, material);

			material.SetTexture("mainTex", TextureOne);
			material.SetTexture("samp1", TextureTwo);
			material.SetTexture("samp2", TextureThree);
		}
	}
}