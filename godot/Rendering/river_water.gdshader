// A river's water around a first-person eye (VISION.md BOD-11; owner's choices: ripples
// flowing downstream, faster where it's steep, white over rapids): a strip along the river
// (RiverWater), UV.x meters downstream and UV.y meters across, COLOR.r its speed (a share of
// the fastest) and COLOR.g its rapids. Clear in the shallows and darker with the depth
// behind it, as the sea's surface (water_surface.gdshader).
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_opaque, shadows_disabled;

uniform sampler2D depth_texture : hint_depth_texture, filter_nearest, repeat_disable;

#include "res://Rendering/water_tint.gdshaderinc"

// The scene's units in meters.
uniform float meters_per_unit = 1.0;

// The ripples repeat this far downstream (RepeatMeters in RiverWater), so each train fits a
// whole number of times.
const float WAVE_REPEAT_METERS = 1000.0;

// The fastest a river flows, in meters a second (RiverProfile.MaxFlow).
const float MAX_FLOW = 6.0;

varying float flow;
varying float rapids;

void vertex() {
	flow = COLOR.r * MAX_FLOW;
	rapids = COLOR.g;
}

// The ripples' slope along and across the river at a point (meters downstream, meters
// across), carried downstream at the flow's speed and faded out with distance so they never
// shimmer.
vec2 ripple_slope(vec2 at, float meters_away) {
	const float ALONG[4] = float[](331.0, 517.0, 863.0, 1291.0);  // Whole cycles a repeat
	const float ACROSS[4] = float[](0.9, -1.7, 2.3, -3.1);         // Radians a meter
	const float HEIGHTS[4] = float[](0.03, 0.02, 0.012, 0.008);
	vec2 slope = vec2(0.0);
	float downstream = at.x - flow * TIME;
	for (int i = 0; i < 4; i++) {
		float k = ALONG[i] * TAU / WAVE_REPEAT_METERS;
		float phase = k * downstream + ACROSS[i] * at.y + TIME * (0.7 + 0.3 * float(i));
		slope += vec2(k, ACROSS[i]) * HEIGHTS[i] * cos(phase);
	}

	// Rapids churn: steeper, shorter waves.
	slope *= 1.0 + 3.0 * rapids;
	return slope * (1.0 - smoothstep(20.0, 300.0, meters_away));
}

// Smooth noise from 0 to 1, varying over about one unit.
float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 cell = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(cell), hash(cell + vec2(1.0, 0.0)), f.x),
		mix(hash(cell + vec2(0.0, 1.0)), hash(cell + vec2(1.0, 1.0)), f.x), f.y);
}

// White water over rapids: patches of foam carried downstream, stretched along the flow;
// farther off, where they'd shimmer, an even whiteness as much as they'd cover.
float foam(vec2 at, float meters_away) {
	float downstream = at.x - flow * TIME;
	float patches = 0.65 * noise(vec2(downstream * 0.3, at.y * 0.7))
		+ 0.35 * noise(vec2(downstream * 0.8, at.y * 1.6 + TIME * 0.4));
	float near = smoothstep(0.5, 0.75, patches);
	return mix(near, 0.35, smoothstep(150.0, 800.0, meters_away)) * rapids + 0.1 * rapids;
}

void fragment() {
	float meters_away = length(VERTEX) * meters_per_unit;
	vec2 slope = ripple_slope(UV, meters_away);
	NORMAL_MAP = normalize(vec3(-slope.x, -slope.y, 1.0)) * 0.5 + 0.5;
	NORMAL_MAP_DEPTH = 1.0;
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float fresnel = 0.04 + 0.96 * pow(1.0 - facing, 5.0);
	vec3 shallow, deep;
	water_shades(vec4(0.0), shallow, deep);

	// How much water the view passes through to the bed behind.
	float depth = texture(depth_texture, SCREEN_UV).r;
	vec4 behind = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
	float through = max(-behind.z / behind.w + VERTEX.z, 0.0) * meters_per_unit;
	float murk = 1.0 - exp(-through / 3.0);
	float white = foam(UV, meters_away);
	ALBEDO = mix(mix(shallow, deep, 1.0 - exp(-through / 15.0)), vec3(0.92), white);
	ALPHA = clamp(max(0.3 + 0.7 * murk, fresnel) + white, 0.0, 0.97);
	ROUGHNESS = mix(0.08, 0.7, white);
	SPECULAR = 0.6;
}
