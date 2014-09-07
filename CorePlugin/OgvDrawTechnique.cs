using System;
using Duality.Drawing;
using Duality.Editor;
using Duality.Resources;

namespace OgvPlayer
{
	[Serializable]
	public class OgvDrawTechnique : DrawTechnique
	{
		[EditorHintFlags(MemberFlags.Invisible)]
		public Texture TextureOne { get; set; }
		
		[EditorHintFlags(MemberFlags.Invisible)]
		public Texture TextureTwo { get; set; }

		[EditorHintFlags(MemberFlags.Invisible)]
		public Texture TextureThree { get; set; }

		public OgvDrawTechnique()
		{
			Blending = BlendMode.Alpha;
		}

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