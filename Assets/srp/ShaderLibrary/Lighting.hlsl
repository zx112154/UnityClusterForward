#ifndef MYRP_LIGHTING_INCLUDED
#define MYRP_LIGHTING_INCLUDED

#define MINIMUM 0.0001

struct LitSurface
{
	float3 normal, position, viewDir;
	float3 diffuse, specular;
	float perceptualRoughness, roughness, fresnelStrength, reflectivity;
	bool perfectDiffuser;
};

LitSurface GetLitSurface(
	float3 normal, float3 position, float3 viewDir,
	float3 color, float metallic, float smoothness, bool perfectDiffuser = false)
{
	LitSurface s;
	s.normal = normal;
	s.position = position;
	s.viewDir = viewDir;
	s.diffuse = color;
	if (perfectDiffuser)
	{
		s.reflectivity = 0.0;
		smoothness = 0.0;
		s.specular = 0.0;
	}
	else
	{
		s.specular = lerp(0.04, color, metallic);
		s.reflectivity = lerp(0.04, 1.0, metallic);
		s.diffuse *= 1.0 - s.reflectivity;
	}
	s.perfectDiffuser = perfectDiffuser;

	//感知粗糙度
	s.perceptualRoughness = 1.0 - smoothness;
	//粗糙度
	s.roughness = s.perceptualRoughness * s.perceptualRoughness;
	//菲涅尔强度
	s.fresnelStrength = saturate(smoothness + s.reflectivity);

	return s;
}

float3 LightSurface(LitSurface s, float3 lightDir)
{
	float3 color = s.diffuse;
	if (!s.perfectDiffuser)
	{
		float3 halfDir = SafeNormalize(lightDir + s.viewDir);
		float nh = saturate(dot(s.normal, halfDir));
		float lh = saturate(dot(lightDir, halfDir));

		float d = nh * nh * (s.roughness * s.roughness - 1.0) + 1.00001;
		float normalizationTerm = s.roughness * 4.0 + 2.0;
		float specularTerm = s.roughness * s.roughness;
		specularTerm /= (d * d) * max(0.1, lh * lh) * normalizationTerm;
		color += specularTerm * s.specular;
	}
	return color * saturate(dot(s.normal, lightDir));
}

LitSurface GetLitSurfaceVertex(float3 normal, float3 position)
{
	return GetLitSurface(normal, position, 0, 1, 0, true);
}

void PremultiplyAlpha(inout LitSurface s, inout float alpha)
{
	s.diffuse *= alpha;
	alpha = lerp(alpha, 1, s.reflectivity);
}

float3 diffuseLighting(float3 lightColor, float3 lightDir, float3 normal, float factor)
{
	float nDot = dot(lightDir, normal);
	return nDot * lightColor * factor;
}


// Unpack normal as DXT5nm (1, y, 1, x) or BC5 (x, y, 0, 1)
float3 UnpackNormalmapRGorAG(float4 pNormal)
{
	pNormal.x *= pNormal.w;
	float3 normal;
	normal.xy = pNormal.xy * 2 - 1;
	normal.z = sqrt(1 - saturate(dot(normal.xy, normal.xy)));
	return normal;
}

//-----------------------------------------BRDF-------------------------------------------------//

//-------------------------------------各向同性--------------------------------------------------//

float3 FrenelSchlick(float3 f0, float hDotV)
{
	float x2 = hDotV * hDotV;
	float value = 1 - x2 * x2 * hDotV;
	return lerp(f0, float3(1.0, 1.0, 1.0), value);
}

float NDFBeckMannDistribution(float nDotH, float roughness)
{
	float x2 = nDotH * nDotH;
	float x4 = x2 * x2;
	float r2 = roughness * roughness;
	float e = exp((x2 - 1) / (r2 * x2));

	return e / (PI * r2  * x4);
}

float NDFBlinnPhong(float nDotH, float roughness)
{
	roughness = pow(8192, roughness);
	return pow(nDotH, roughness) * (roughness + 2) * (0.5 * INV_PI);
}

float NDFGGX(float nDotH, float roughness)
{
	float g = roughness * roughness;
	float g2 = g * g;

	float d = 1 + (nDotH * nDotH) * (g2 - 1);
	float d2 = d * d;

	return g2 * INV_PI / d2;
}

//tail 控制NDF形状，特别是尾部：虚查表推导G函数，不使用
float NDFGtr(float nDotH, float roughness, float tail)
{
	//计算k值
	float r2 = roughness * roughness;
	float t1 = (r2 - 1) * (tail - 1) / (1 - pow(r2, 1 - tail));
	float t2 = (r2 - 1) / log(r2);
	float k = tail != 1 && roughness != 1 ? t1 : t2;
	k = roughness == 1 ? 1 : k;

	float p = 1 + nDotH * nDotH * (r2 - 1);

	return k / (PI * pow(p, tail));
}

//粗糙度控制高斯函数的宽度，kAmp控制振幅
float NDFClothMicrofacet(float kAmp, float roughness, float nDotH)
{
	float h2 = nDotH * nDotH;
	float r2 = roughness * roughness;

	float d1 = PI * (1 + kAmp * r2);
	float d2 = (1 - h2) * (1 - h2);

	return 1 / d1 * (1 + kAmp / d2 * exp(h2 / (r2 * (h2 - 1))));
}

float NDFClothMicrofacetImageWorks(float roughness, float nDotH)
{
	float h2 = nDotH * nDotH;
	float invR = 1 / roughness;

	return nDotH * (2 + invR) * pow(1 - h2, invR * 0.5) * 0.5 * INV_PI;
}


float MaskCookTorranceCustom(float NdotH, float NdotV, float NdotL, float HdotV)
{
	//float g1 = 2 * nDotH * nDotL / HdotV;
	//float g2 = 2 * nDotH * nDotv/ HdotV;
	//return min(1, g1, g2);

	return min(1.0, 2.0 * NdotH * min(NdotV, NdotL) / HdotV);
}

//V(l,v) = G(l, v) / (4 * dot(n, l) * dot(n, v))
float VisibleGGX(float nDotL, float nDotV, float roughness)
{
	float r2 = roughness * roughness;
	float lambda_v = nDotV * (r2 + nDotL * (nDotL - r2 * nDotL));
	float lambda_l = nDotL * (r2 + nDotV * (nDotV - r2 * nDotV));

	return 0.5 / (lambda_v + lambda_l);
}

float VisibleGGXKaris(float nDotL, float nDotV, float roughness)
{
	return 0.5 / lerp(2 * (nDotL * nDotV), nDotL + nDotV, roughness);
}

//-------------------------------------各向异性--------------------------------------------------//

float3 ReMappingTangent(float3 t, float3 n)
{
	return t - dot(t, n) * n;
}

float2 GetDisneyRoughnessXY(float roughness, float kAniso)
{
	float kAspect = sqrt(1 - 0.9 * kAniso);
	float r2 = roughness * roughness;
	return float2(r2 / kAspect, r2 * kAspect);
}

float2 GetImageworkRoughnessXY(float roughness, float kAniso)
{
	float r2 = roughness * roughness;
	return float2(r2 * (1 + kAniso), r2 * (1 - kAniso));
}

float NDFBeckMannDistribution_Anisotropic(float nDotH, float tDotH, float bDotH, float roughness, float kAniso)
{
	float t2 = tDotH * tDotH;
	float b2 = bDotH * bDotH;
	float m2 = nDotH * nDotH;

	float2 r = GetImageworkRoughnessXY(roughness, kAniso);

	float rX2 = r.x * r.x;
	float rY2 = r.y * r.y;

	float e = (t2 / rX2 + b2 / rY2) / -m2;

	return 1 / (PI * r.x * r.y * m2 * m2) * exp(e);
}

float NDFGGX_Anisotropic(float nDotH, float tDotH, float bDotH, float roughness, float kAniso)
{
	float t2 = tDotH * tDotH;
	float b2 = bDotH * bDotH;

	float2 r = GetImageworkRoughnessXY(roughness, kAniso);

	float rX2 = r.x * r.x;
	float rY2 = r.y * r.y;

	float d = t2 / rX2 + b2 / rY2 + nDotH * nDotH;
	d *= d;

	return 1 / (PI * r.x * r.y * d);
}

//----------------------------------------diffuse terms--------------------------------------------------------//

float3 DiffuseDisney(float3 cDiff)
{
	return cDiff * INV_PI;
}

float3 DiffuseShirley(float3 pss, float3 f0, float nDotL, float nDotV)
{
	float v1 = 1 - nDotV;
	float l1 = 1 - nDotL;
	float v2 = v1 * v1;
	float l2 = l1 * l1;

	float v = 1 - v1 * v2 * v2;
	float l = 1 - l1 * l2 * l2;

	return 21 / (20 * PI) * (1 - f0) * pss * v * l;
}

//rough-surface subsurface models
float3 DiffuseBurley(float3 pss, float hDotL, float nDotL, float nDotV, float roughness, float kss)
{
	float h2 = hDotL * hDotL;
	float fL = 1 - nDotL;
	float fV = 1 - nDotV;
	float fL2 = fL * fL;
	float fV2 = fV * fV;
	float fL5 = fL2 * fL2 * fL;
	float fV5 = fV2 * fV2 * fV;
	float sR = sqrt(roughness);

	float Ffss90 = h2 * sR;
	float Fss = (1 + (Ffss90 - 1) * fL5) * (1 + (Ffss90 - 1) * fV5);	//全局地面散射

	float fss = (1 / (nDotL * nDotV) - 0.5) * Fss + 0.5;
	float FD90 = 0.5 + 2 * sR * h2;

	float fd = (1 + (FD90 - 1) * fL5) * (1 + (FD90 - 1) * fV5);

	return pss * INV_PI *((1 - kss) * fd + 1.25 * kss * fss);
}

//带二次反射的diffuse term
float3 DiffuseHammon(float nDotL, float lDotV, float nDotH, float roughness, float3 pss, float3 f0)
{
	float fL = 1 - nDotL;
	float fV = 1 - nDotL;
	float fL2 = fL * fL;
	float fV2 = fV * fV;
	float fL5 = fL2 * fL2 * fL;
	float fV5 = fV2 * fV2 * fV;

	float fMulti = 0.3641 * roughness;
	float kFacing = 0.5 + 0.5 * lDotV;

	float fRough = kFacing * (0.9 - 0.4 * kFacing) * ((0.5 + nDotH) / nDotH);

	float fSmooth = 21 / 20 * (1 - f0) * (1 - fL5) * (1 - fV5);

	return pss * INV_PI * ((1 - roughness) * fSmooth + roughness * fRough + pss * fMulti);
}

float3 DiffuseClothMicrofacet(float3 pss, float3 f0, float nDotL, float hDotL, float nDotV, float nDotH, float kAmp, float roughness)
{
	float3 f = FrenelSchlick(f0, hDotL);

	float determin = 4 * (nDotL + nDotV - nDotL * nDotV);

	float d = NDFClothMicrofacet(kAmp, roughness, nDotH);

	return (1 - f) * pss * INV_PI + f * d / determin;
}

//------------------------------------------------------------------------------------------------//

struct SPBRParams
{
	float3 albedo;		//漫反射颜色
	float3 lightDir;	//光源方向
	float3 normal;		//表面法线
	float3 viewDir;		//相机方向
	float roughness;	//粗糙度
	float3 f0;			//菲涅尔0度角的值
	float metallic;		//金属性
	float3 radiance;	//光的辐照度
	float3 specColor;	//高光颜色
};

struct SPBRAnisParams
{
	float3 albedo;		//漫反射颜色
	float3 lightDir;	//光源方向
	float3 normal;		//表面法线
	float3 tangent;		//表面切线
	float3 viewDir;		//相机方向
	float roughness;	//粗糙度
	float3 f0;			//菲涅尔0度角的值
	float metallic;		//金属性
	float3 radiance;	//光的辐照度
	float3 specColor;	//高光颜色
	float kAniso;		//各向异性
};

//F * G * D / 4 * dot(n, l)(n, v)
//I = Id + Is + Ia = KdId + Ia + IlKsRs (Il光强, Ks反射系数, Rs高光项, Kd漫反射系数)
float3 BrdfCookTorrance(SPBRParams _params)
{
	float3 h = SafeNormalize(_params.lightDir + _params.viewDir);
	float nDotL = saturate(dot(_params.normal, _params.lightDir));
	float nDotV = saturate(dot(_params.normal, _params.viewDir));
	float nDotH = saturate(dot(_params.normal, h));
	float hDotV = saturate(dot(h, _params.viewDir));
	float lDotH = saturate(dot(_params.lightDir, h));
	float lDotV = saturate(dot(_params.lightDir, _params.viewDir));
	float hDotL = saturate(dot(h, _params.lightDir));

	//Frenel
	float3 f0 = lerp(_params.f0, _params.albedo, _params.metallic);
	float3 f = FrenelSchlick(f0, hDotV);
	f = max(f, f0);

	float roughness = _params.roughness * _params.roughness;
	roughness = max(roughness, MINIMUM);

	//D
	//float d = NDFBeckMannDistribution(nDotH, _params.roughness);
	//float d = NDFBlinnPhong(nDotH, _params.roughness);
	float d = NDFGGX(nDotH, _params.roughness);

	////factor 校正因子
	//float denominator = (4 * nDotL * nDotV);

	////G(l, v, h) / (dot(n,l) * dot(n, v))
	//float v = MaskCookTorranceCustom(nDotH, nDotV, nDotL, hDotV) / denominator;

	//float v = VisibleGGX(nDotL, nDotV, roughness);
	float v = VisibleGGXKaris(nDotL, nDotV, roughness);

	float3 Rs = f * d * v;//max(denominator, MINIMUM);

	float3 ks = f;

	//diffuse terms;
	float3 kd = 1 - ks;

	kd *= 1.0 - _params.metallic;

	float3 Id = DiffuseDisney(_params.albedo) * kd;

	//float3 Id = DiffuseShirley(_params.albedo, f0, nDotL, nDotV);

	//float3 Id = DiffuseBurley(_params.albedo, lDotH, nDotL, nDotV, roughness, _params.metallic);

	//float3 Id = DiffuseHammon(nDotL, lDotV, nDotH, roughness, _params.albedo, _params.f0);

	//float3 Id = DiffuseClothMicrofacet(_params.albedo, _params.f0, nDotL, hDotL, nDotV, nDotH, _params.metallic, roughness);

	return (Id + Rs * _params.specColor) * _params.radiance * nDotL;
}

float3 BrdfLight(SPBRParams _params)
{
	return BrdfCookTorrance(_params);
}

float3 BrdfCookTorrance_Anis(SPBRAnisParams _params)
{
	float3 h = SafeNormalize(_params.lightDir + _params.viewDir);
	float nDotL = saturate(dot(_params.normal, _params.lightDir));
	float nDotV = saturate(dot(_params.normal, _params.viewDir));
	float nDotH = saturate(dot(_params.normal, h));
	float hDotV = saturate(dot(h, _params.viewDir));
	float lDotH = saturate(dot(_params.lightDir, h));
	float lDotV = saturate(dot(_params.lightDir, _params.viewDir));

	//重新映射切线
	float3 t = ReMappingTangent(_params.tangent, _params.normal);
	float3 bt = cross(_params.normal, t);

	float tDotH = saturate(dot(t, h));
	float bDotH = saturate(dot(bt, h));

	//Frenel
	float3 f0 = lerp(_params.f0, _params.albedo, _params.metallic);
	float3 f = FrenelSchlick(f0, nDotH);
	f = max(f, f0);

	float roughness = _params.roughness * _params.roughness;
	roughness = max(roughness, MINIMUM);

	//D
	//float d = NDFBeckMannDistribution(nDotH, _params.roughness);
	//float d = NDFBlinnPhong(nDotH, _params.roughness);
	float d = NDFGGX_Anisotropic(nDotH, tDotH, bDotH, roughness, _params.kAniso);

	////factor 校正因子
	//float denominator = (4 * nDotL * nDotV);

	////G(l, v, h) / (dot(n,l) * dot(n, v))
	//float v = MaskCookTorranceCustom(nDotH, nDotV, nDotL, hDotV) / denominator;

	//float v = VisibleGGX(nDotL, nDotV, roughness);
	float v = VisibleGGXKaris(nDotL, nDotV, roughness);

	float3 Rs = f * d * v;//max(denominator, MINIMUM);

	float3 ks = f;

	float3 kd = 1 - ks;

	kd *= 1.0 - _params.metallic;

	//float3 Id = DiffuseDisney(_params.albedo) * kd;

	//float3 Id = DiffuseShirley(_params.albedo, f0, nDotL, nDotV);

	//各向异性使用XY粗糙度的中间值
	float2 rXY = GetImageworkRoughnessXY(roughness, _params.kAniso);
	float3 Id = DiffuseBurley(_params.albedo, lDotH, nDotL, nDotV, (rXY.x + rXY.y) * 0.5, _params.metallic);
	//float3 Id = DiffuseHammon(nDotL, lDotV, nDotH, rXY, _params.albedo, _params.f0);


	return (Id + Rs * _params.specColor) * _params.radiance * nDotL;
}



//-----------------------------------------BRDF-------------------------------------------------//

#endif //MYRP_LIGHTING_INCLUDED