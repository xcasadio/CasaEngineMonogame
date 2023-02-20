uniform highp mat4 ModelViewProj;

#ifdef GLES2

attribute highp vec3 vPositionIn;
attribute highp vec3 vNormalIn;
attribute mediump vec2 vTextCoordIn;

varying mediump vec2 texCoord;

#else

layout(location = 0) in vec3 vPositionIn;
layout(location = 1) in vec3 vNormalIn;
layout(location = 2) in vec2 vTextCoordIn;

out vec2 texCoord;

#endif

void main()
{
	gl_Position = ModelViewProj * vec4(vPositionIn, 1.0);
	texCoord = vTextCoordIn;
}
