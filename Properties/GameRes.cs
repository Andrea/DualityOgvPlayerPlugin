/*
 * A set of static helper classes that provide easy runtime access to the games resources.
 * This file is auto-generated. Any changes made to it are lost as soon as Duality decides
 * to regenerate it.
 */
namespace GameRes
{
	public static class Data {
		public static Duality.ContentRef<Duality.Resources.AudioData> AudioData_AudioData { get { return Duality.ContentProvider.RequestContent<Duality.Resources.AudioData>(@"Data\AudioData.AudioData.res"); }}
		public static Duality.ContentRef<Duality.Resources.Sound> Sound_Sound { get { return Duality.ContentProvider.RequestContent<Duality.Resources.Sound>(@"Data\Sound.Sound.res"); }}
		public static void LoadAll() {
			AudioData_AudioData.MakeAvailable();
			Sound_Sound.MakeAvailable();
		}
	}

}
