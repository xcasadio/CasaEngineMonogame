uniform sampler2D Texture0;
uniform vec2 TexRepeat;

#ifdef GLES2

varying mediump vec2 texCoord;

lowp vec4 color;

#else

in vec2 texCoord;

layout(location = 0) out vec4 color;

#endif


void main()
{
	color = texture(Texture0, texCoord * TexRepeat);

#ifdef GLES2
	gl_FragColor = color;
#endif
}
