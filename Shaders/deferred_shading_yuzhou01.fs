#version 330
//Notes: LightT or lightT below means that paramenter is referenced from the deferred_shading tutorial
out vec4 FragColor;//Shao Added for deferred_shading

in vec4 vCol;
in vec2 TexCoord;//In the tutorial, this parameter is TexCoords, therefore, all the TexCoords, from the tutorial below, should be changed to TexCoord accordingly.

//in the 8.2.deferred_shading,above is TexCoords, it should be united later.
in vec3 Normal;
in vec3 FragPos;
in vec4 DirectionalLightSpacePos;

//out vec4 colour;//Marked

const int MAX_POINT_LIGHTS = 3;
const int MAX_SPOT_LIGHTS = 3;


uniform sampler2D gPosition;//Shao added for deferred_shading
uniform sampler2D gNormal;//Shao added for deferred_shading
uniform sampler2D gAlbedoSpec;//Shao added for deferred_shading


struct Light
{
	//vec3 Position;//Shao added as 8.2.deferred_shading.fs, later deleted because of the clash with line 22 to 24 
	vec3 colour;
	float ambientIntensity;
	float diffuseIntensity;
	
	//Shao added below lines,later deleted because of the clash with line 22 to 24 
	//float Linear;
    //float Quadratic;
    //float Radius;
	
	vec3 Position;//Shao added for deferred_shading
    vec3 Color;//Shao added for deferred_shading
    
    float Linear;//Shao added for deferred_shading
    float Quadratic;//Shao added for deferred_shading
    float Radius;//Shao added for deferred_shading
};

/*//Removed because there is a better solution in above struct.
//Line57 uniform Light lights[NR_LIGHTS]; should also be correspondingly 
//changed from  previously uniform LightT lights[NR_LIGHTS];
struct LightT { //Shao added for deferred_shading
    vec3 Position;//Shao added for deferred_shading
    vec3 Color;//Shao added for deferred_shading
    
    float Linear;//Shao added for deferred_shading
    float Quadratic;//Shao added for deferred_shading
    float Radius;//Shao added for deferred_shading
};//Shao added for deferred_shading
*/

const int NR_LIGHTS = 32;//Shao added for deferred_shading
uniform Light lights[NR_LIGHTS];//Shao added for deferred_shading
uniform vec3 viewPos;//Shao added for deferred_shading

struct DirectionalLight 
{
	Light base;
	vec3 direction;
};

struct PointLight
{
	Light base;
	vec3 position;
	float constant;
	float linear;
	float exponent;
};

struct SpotLight
{
	PointLight base;
	vec3 direction;
	float edge;
};

struct OmniShadowMap
{
	samplerCube shadowMap;
	float farPlane;
};

struct Material
{
	float specularIntensity;
	float shininess;
};

uniform int pointLightCount;
uniform int spotLightCount;

uniform DirectionalLight directionalLight;
uniform PointLight pointLights[MAX_POINT_LIGHTS];
uniform SpotLight spotLights[MAX_SPOT_LIGHTS];

uniform sampler2D theTexture;
uniform sampler2D directionalShadowMap;
uniform OmniShadowMap omniShadowMaps[MAX_POINT_LIGHTS + MAX_SPOT_LIGHTS];

uniform Material material;

uniform vec3 eyePosition;

vec3 sampleOffsetDirections[20] = vec3[]
(
   vec3( 1,  1,  1), vec3( 1, -1,  1), vec3(-1, -1,  1), vec3(-1,  1,  1), 
   vec3( 1,  1, -1), vec3( 1, -1, -1), vec3(-1, -1, -1), vec3(-1,  1, -1),
   vec3( 1,  1,  0), vec3( 1, -1,  0), vec3(-1, -1,  0), vec3(-1,  1,  0),
   vec3( 1,  0,  1), vec3(-1,  0,  1), vec3( 1,  0, -1), vec3(-1,  0, -1),
   vec3( 0,  1,  1), vec3( 0, -1,  1), vec3( 0, -1, -1), vec3( 0,  1, -1)
); 

float CalcDirectionalShadowFactor(DirectionalLight light)
{
	vec3 projCoords = DirectionalLightSpacePos.xyz / DirectionalLightSpacePos.w;
	projCoords = (projCoords * 0.5) + 0.5;
	
	float current = projCoords.z;
	
	vec3 normal = normalize(Normal);
	vec3 lightDir = normalize(directionalLight.direction);
	
	float bias = max(0.05 * (1.0 - dot(normal, lightDir)), 0.0005);

	
	float shadow = 0.0;
	vec2 texelSize = 1.0 / textureSize(directionalShadowMap, 0);
	for(int x = -1; x <= 1; ++x)
	{
		for(int y = -1; y <= 1; ++y)
		{
			float pcfDepth = texture(directionalShadowMap, projCoords.xy + vec2(x,y) * texelSize).r;
			shadow += current - bias > pcfDepth ? 1.0 : 0.0;
		}
	}

	shadow /= 9.0;
	
	if(projCoords.z > 1.0)
	{
		shadow = 0.0;
	}									
	
	return shadow;
}

float CalcOmniShadowFactor(PointLight light, int shadowIndex)
{
	vec3 fragToLight = FragPos - light.position;
	float currentDepth = length(fragToLight);
	
	float shadow = 0.0;
	float bias = 0.05;
	int samples = 20;
	
	float viewDistance = length(eyePosition - FragPos);
	float diskRadius = (1.0 + (viewDistance/omniShadowMaps[shadowIndex].farPlane)) / 25.0;
	
	for(int i = 0; i < samples; i++)
	{
		float closestDepth = texture(omniShadowMaps[shadowIndex].shadowMap, fragToLight + sampleOffsetDirections[i] * diskRadius).r;
		closestDepth *= omniShadowMaps[shadowIndex].farPlane;
		if(currentDepth -  bias > closestDepth)
		{
			shadow += 1.0;
		}
	}
	
	
	shadow /= float(samples);
	return shadow;
}

vec4 CalcLightByDirection(Light light, vec3 direction, float shadowFactor)
{
	vec4 ambientColour = vec4(light.colour, 1.0f) * light.ambientIntensity;
	
	float diffuseFactor = max(dot(normalize(Normal), normalize(direction)), 0.0f);
	vec4 diffuseColour = vec4(light.colour * light.diffuseIntensity * diffuseFactor, 1.0f);
	
	vec4 specularColour = vec4(0, 0, 0, 0);
	
	if(diffuseFactor > 0.0f)
	{
		vec3 fragToEye = normalize(eyePosition - FragPos);
		vec3 reflectedVertex = normalize(reflect(direction, normalize(Normal)));
		
		float specularFactor = dot(fragToEye, reflectedVertex);
		if(specularFactor > 0.0f)
		{
			specularFactor = pow(specularFactor, material.shininess);
			specularColour = vec4(light.colour * material.specularIntensity * specularFactor, 1.0f);
		}
	}

	return (ambientColour + (1.0 - shadowFactor) * (diffuseColour + specularColour));
}

vec4 CalcDirectionalLight()
{
	float shadowFactor = CalcDirectionalShadowFactor(directionalLight);
	return CalcLightByDirection(directionalLight.base, directionalLight.direction, shadowFactor);
}

vec4 CalcPointLight(PointLight pLight, int shadowIndex)
{
	vec3 direction = FragPos - pLight.position;
	float distance = length(direction);
	direction = normalize(direction);
	
	float shadowFactor = CalcOmniShadowFactor(pLight, shadowIndex);
	
	vec4 colour = CalcLightByDirection(pLight.base, direction, shadowFactor);
	float attenuation = pLight.exponent * distance * distance +
						pLight.linear * distance +
						pLight.constant;
	
	return (colour / attenuation);
}

vec4 CalcSpotLight(SpotLight sLight, int shadowIndex)
{
	vec3 rayDirection = normalize(FragPos - sLight.base.position);
	float slFactor = dot(rayDirection, sLight.direction);
	
	if(slFactor > sLight.edge)
	{
		vec4 colour = CalcPointLight(sLight.base, shadowIndex);
		
		return colour * (1.0f - (1.0f - slFactor)*(1.0f/(1.0f - sLight.edge)));
		
	} else {
		return vec4(0, 0, 0, 0);
	}
}

vec4 CalcPointLights()
{
	vec4 totalColour = vec4(0, 0, 0, 0);
	for(int i = 0; i < pointLightCount; i++)
	{		
		totalColour += CalcPointLight(pointLights[i], i);
	}
	
	return totalColour;
}

vec4 CalcSpotLights()
{
	vec4 totalColour = vec4(0, 0, 0, 0);
	for(int i = 0; i < spotLightCount; i++)
	{		
		totalColour += CalcSpotLight(spotLights[i], i + pointLightCount);
	}
	
	return totalColour;
}

void main()
{
	vec4 finalColour = CalcDirectionalLight();
	finalColour += CalcPointLights();
	finalColour += CalcSpotLights();
	
	
	//Shao added from below
	
	// retrieve data from gbuffer
    //vec3 FragPos = texture(gPosition, TexCoords).rgb;//The origial code from the tutorial 8.2deferred_shading
    vec3 FragPos = texture(gPosition, TexCoord).rgb;//Shao added for deferred_shading
	//vec3 Normal = texture(gNormal, TexCoords).rgb;//The origial code from the tutorial 8.2deferred_shading
    vec3 Normal = texture(gNormal, TexCoord).rgb;//Shao added for deferred_shading
	//vec3 Diffuse = texture(gAlbedoSpec, TexCoords).rgb;//The origial code from the tutorial 8.2deferred_shading
    vec3 Diffuse = texture(gAlbedoSpec, TexCoord).rgb;//Shao added for deferred_shading
	//float Specular = texture(gAlbedoSpec, TexCoords).a;//The origial code from the tutorial 8.2deferred_shading
    float Specular = texture(gAlbedoSpec, TexCoord).a;//Shao added for deferred_shading
	
	
    // then calculate lighting as usual
    vec3 lighting  = Diffuse * 0.1; // hard-coded ambient component
    vec3 viewDir  = normalize(viewPos - FragPos);
    for(int i = 0; i < NR_LIGHTS; ++i)
    {
        // diffuse
        vec3 lightDir = normalize(lights[i].Position - FragPos);
        vec3 diffuse = max(dot(Normal, lightDir), 0.0) * Diffuse * lights[i].Color;
        // specular
        vec3 halfwayDir = normalize(lightDir + viewDir);  
        float spec = pow(max(dot(Normal, halfwayDir), 0.0), 16.0);
        vec3 specular = lights[i].Color * spec * Specular;
        // attenuation
        float distance = length(lights[i].Position - FragPos);
        float attenuation = 1.0 / (1.0 + lights[i].Linear * distance + lights[i].Quadratic * distance * distance);
        diffuse *= attenuation;
        specular *= attenuation;
        lighting += diffuse + specular;        
    }
	/**///Shao added up to above
	
	colour = texture(theTexture, TexCoord) * finalColour;
	FragColor = vec4(lighting, 1.0);
}