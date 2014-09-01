/*
 * A set of static helper classes that provide easy runtime access to the games resources.
 * This file is auto-generated. Any changes made to it are lost as soon as Duality decides
 * to regenerate it.
 */
namespace GameRes
{
	public static class Data {
		public static Duality.ContentRef<Duality.Resources.AudioData> _320x240_AudioData { get { return Duality.ContentProvider.RequestContent<Duality.Resources.AudioData>(@"Data\320x240.AudioData.res"); }}
		public static Duality.ContentRef<Duality.Resources.Scene> Scene_Scene { get { return Duality.ContentProvider.RequestContent<Duality.Resources.Scene>(@"Data\Scene.Scene.res"); }}
		public static void LoadAll() {
			_320x240_AudioData.MakeAvailable();
			Scene_Scene.MakeAvailable();
		}
	}

}
