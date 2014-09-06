using System;
using Duality;
using Duality.Resources;
using OpenTK.Input;

namespace OgvPlayer
{
    [Serializable]
    public class VideoPlayerComponent : Component, ICmpUpdatable
    {
        private OgvComponent _ogvComponent;

	    public void OnUpdate()
        {
            _ogvComponent = _ogvComponent ?? Scene.Current.FindComponent<OgvComponent>();
            if (DualityApp.Keyboard.KeyReleased(Key.Q)&&_ogvComponent.State != MediaState.Playing)
            {
                _ogvComponent.Play();
				Log.Editor.Write("Play");
	            
            }
            if (DualityApp.Keyboard.KeyReleased(Key.O))
            {
                _ogvComponent.Stop();
            }
        }
    }
}
