using System;
using Duality.Resources;

namespace OgvPlayer
{
	[Serializable]
	public class OgvFragmentShader : FragmentShader
	{
		private const string Shader = @"#version 110

uniform sampler2D mainTex;
uniform sampler2D samp1;
uniform sampler2D samp2;

const vec3 offset = vec3(-0.0625, -0.5, -0.5);
const vec3 Rcoeff = vec3(1.164,  0.000,  1.596);
const vec3 Gcoeff = vec3(1.164, -0.391, -0.813);
const vec3 Bcoeff = vec3(1.164,  2.018,  0.000);

void main() 
{
   vec2 tcoord;
   vec3 yuv, rgb;
   
   tcoord = gl_TexCoord[0].xy;
   
   yuv.x = texture2D(mainTex, tcoord).r;
   yuv.y = texture2D(samp1, tcoord).r;
   yuv.z = texture2D(samp2, tcoord).r;
   yuv += offset;
   
   rgb.r = dot(yuv, Rcoeff);
   rgb.g = dot(yuv, Gcoeff);
   rgb.b = dot(yuv, Bcoeff);
   
   gl_FragColor = vec4(rgb, 1.0);
}";

		public OgvFragmentShader()
		{
			this.Source = Shader;
		}
	}
}