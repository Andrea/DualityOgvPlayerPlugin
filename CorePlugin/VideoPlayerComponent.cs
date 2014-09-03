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
            if (DualityApp.Keyboard.KeyPressed(Key.P))
            {

                _ogvComponent.Play();
            }
            if (DualityApp.Keyboard.KeyPressed(Key.O))
            {

                _ogvComponent.Stop();
            }
        }
    }
}
