/*
 * A set of static helper classes that provide easy runtime access to the games resources.
 * This file is auto-generated. Any changes made to it are lost as soon as Duality decides
 * to regenerate it.
 */
namespace GameRes
{
	public static class Data {
		public static Duality.ContentRef<OgvPlayer.OgvDrawTechnique> OgvDrawTechnique_OgvDrawTechnique { get { return Duality.ContentProvider.RequestContent<OgvPlayer.OgvDrawTechnique>(@"Data\OgvDrawTechnique.OgvDrawTechnique.res"); }}
		public static Duality.ContentRef<Duality.Resources.FragmentShader> OgvFragmentShader_FragmentShader { get { return Duality.ContentProvider.RequestContent<Duality.Resources.FragmentShader>(@"Data\OgvFragmentShader.FragmentShader.res"); }}
		public static Duality.ContentRef<Duality.Resources.Material> OgvMaterial_Material { get { return Duality.ContentProvider.RequestContent<Duality.Resources.Material>(@"Data\OgvMaterial.Material.res"); }}
		public static Duality.ContentRef<Duality.Resources.ShaderProgram> OgvShader_ShaderProgram { get { return Duality.ContentProvider.RequestContent<Duality.Resources.ShaderProgram>(@"Data\OgvShader.ShaderProgram.res"); }}
		public static Duality.ContentRef<Duality.Resources.Texture> OgvTexture_01_Texture { get { return Duality.ContentProvider.RequestContent<Duality.Resources.Texture>(@"Data\OgvTexture-01.Texture.res"); }}
		public static Duality.ContentRef<Duality.Resources.Texture> OgvTexture_02_Texture { get { return Duality.ContentProvider.RequestContent<Duality.Resources.Texture>(@"Data\OgvTexture-02.Texture.res"); }}
		public static Duality.ContentRef<Duality.Resources.Texture> OgvTexture_03_Texture { get { return Duality.ContentProvider.RequestContent<Duality.Resources.Texture>(@"Data\OgvTexture-03.Texture.res"); }}
		public static Duality.ContentRef<Duality.Resources.VertexShader> OgvVertexShader_VertexShader { get { return Duality.ContentProvider.RequestContent<Duality.Resources.VertexShader>(@"Data\OgvVertexShader.VertexShader.res"); }}
		public static Duality.ContentRef<Duality.Resources.Scene> Scene__2__Scene { get { return Duality.ContentProvider.RequestContent<Duality.Resources.Scene>(@"Data\Scene (2).Scene.res"); }}
		public static Duality.ContentRef<Duality.Resources.Scene> Scene_Scene { get { return Duality.ContentProvider.RequestContent<Duality.Resources.Scene>(@"Data\Scene.Scene.res"); }}
		public static void LoadAll() {
			OgvDrawTechnique_OgvDrawTechnique.MakeAvailable();
			OgvFragmentShader_FragmentShader.MakeAvailable();
			OgvMaterial_Material.MakeAvailable();
			OgvShader_ShaderProgram.MakeAvailable();
			OgvTexture_01_Texture.MakeAvailable();
			OgvTexture_02_Texture.MakeAvailable();
			OgvTexture_03_Texture.MakeAvailable();
			OgvVertexShader_VertexShader.MakeAvailable();
			Scene__2__Scene.MakeAvailable();
			Scene_Scene.MakeAvailable();
		}
	}

}
