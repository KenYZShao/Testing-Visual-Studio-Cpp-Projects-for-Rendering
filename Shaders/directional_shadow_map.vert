//This code is basically studied followed from Udemy https://www.udemy.com/course/graphics-with-modern-opengl/
#version 330

layout (location = 0) in vec3 pos;

uniform mat4 model;
uniform mat4 directionalLightTransform;

void main()
{
	gl_Position = directionalLightTransform * model * vec4(pos, 1.0);
}
