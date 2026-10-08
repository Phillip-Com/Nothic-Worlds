// The water's surface around a first-person eye (VISION.md BOD-09): rings of mesh at the
// body's water level around the spot (a FirstPersonGround built at a set radius). From above
// it's clear in the shallows and darkens with the water's depth below it (read from the depth
// of what's behind it), shining in the sun; from below it's a bright, see-through ceiling.
// Small waves move across it.
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_opaque, shadows_disabled;

uniform sampler2D depth_texture : hint_depth_texture, filter_nearest, repeat_disable;

// The globe's terrain and water colors (PlanetSurface.CopyWaterColorsTo), for the water's
// color over each kind of terrain.
uniform bool has_terrain = false;
uniform sampler2DArray terrain_cells : filter_nearest, repeat_disable;
uniform sampler2D water_palette : source_color, filter_nearest, repeat_disable;

#include "res://Rendering/water_tint.gdshaderinc"

// The scene's units in meters, and the body's radius in meters (the mesh's units are radii).
uniform float meters_per_unit = 1.0;
uniform float radius_meters = 6371000.0;

// Where the mesh's middle falls in the waves' repeat (WaveRepeatMeters in FirstPersonMode),
// worked out on the CPU in double precision, so the waves stay put as the mesh is rebuilt.
uniform vec3 wave_origin = vec3(0.0);

// How light the day is, from 0 at night to 1 (SurfaceSky.Daylight): the surface seen from
// below glows with the light coming through it.
uniform float daylight = 1.0;

// On a flat world (VISION.md BOD-10), the surface lies over the top face: up is the face's,
// and each point brings its place on the disc (CUSTOM0) rather than a direction.
uniform bool flat_disc = false;

const float WAVE_REPEAT_METERS = 1000.0;

varying vec3 up;  // The point's direction from the body's middle, or its place on the disc
varying vec3 wave_place;

void vertex() {
	up = CUSTOM0.xyz;
	wave_place = VERTEX * radius_meters + wave_origin;
}

// The waves' slope here, in meters per meter along the globe's axes: four trains of waves a
// few meters long, each a whole number of times along the repeat on every axis so they
// repeat exactly, faded out with distance so they never shimmer.
vec3 wave_slope(vec3 q, float meters_away) {
	const vec3 TRAINS[4] = vec3[](vec3(131.0, -57.0, 89.0), vec3(-97.0, 163.0, 41.0),
		vec3(211.0, -73.0, -151.0), vec3(-59.0, -241.0, 173.0));
	const float HEIGHTS[4] = float[](0.05, 0.04, 0.025, 0.02);
	vec3 slope = vec3(0.0);
	for (int i = 0; i < 4; i++) {
		vec3 k = TRAINS[i] * (TAU / WAVE_REPEAT_METERS);
		float phase = dot(k, q) + TIME * (1.1 + 0.37 * float(i));
		slope += k * HEIGHTS[i] * cos(phase);
	}

	return slope * (1.0 - smoothstep(30.0, 400.0, meters_away));
}

void fragment() {
	vec3 normal_up = flat_disc ? vec3(0.0, 1.0, 0.0) : normalize(up);
	vec3 on_map = flat_disc ? flat_disc_direction(up) : normal_up;
	float meters_away = length(VERTEX) * meters_per_unit;
	vec3 slope = wave_slope(wave_place, meters_away);
	slope -= normal_up * dot(slope, normal_up);
	vec3 normal = normalize(normal_up - slope);
	vec3 view_normal = normalize((VIEW_MATRIX * MODEL_MATRIX * vec4(normal, 0.0)).xyz);
	NORMAL = FRONT_FACING ? view_normal : -view_normal;
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float fresnel = 0.04 + 0.96 * pow(1.0 - facing, 5.0);
	ROUGHNESS = 0.08;
	SPECULAR = 0.6;
	vec4 tint = has_terrain ? water_tint(terrain_cells, water_palette, on_map) : vec4(0.0);
	vec3 shallow, deep;
	water_shades(tint, shallow, deep);

	if (FRONT_FACING) {
		// How much water the view passes through to the ground behind: deeper looks darker
		// and hides the bottom.
		float depth = texture(depth_texture, SCREEN_UV).r;
		vec4 behind = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
		float through = max(-behind.z / behind.w + VERTEX.z, 0.0) * meters_per_unit;
		float murk = 1.0 - exp(-through / 6.0);
		ALBEDO = mix(shallow, deep, 1.0 - exp(-through / 40.0));
		ALPHA = clamp(max(0.25 + 0.75 * murk, fresnel), 0.0, 0.97);
	} else {
		// Looking up from under the water: lit from above, so it glows rather than being
		// lit; brightest straight overhead, where the light comes through, and turning to a
		// darker mirror of the water toward the edge.
		ALBEDO = vec3(0.0);
		SPECULAR = 0.0;
		vec3 through = min(shallow * 1.8, vec3(1.0)) * (0.3 + 0.7 * facing);
		EMISSION = mix(through, shallow * 0.5, fresnel) * daylight;
		ALPHA = mix(0.6, 0.95, fresnel);
	}
}
