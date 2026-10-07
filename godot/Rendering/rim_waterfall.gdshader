// The water pouring over a flat world's rim (VISION.md BOD-10; owner's choice: a waterfall
// over the edge): a curtain hung all the way round from the rim's top edge. It shoots out a
// little as it falls, streams down in streaks, and turns to mist that fades away well below
// the disc. Its mesh (RimWaterfall) is a ring of points with UV.y from 0 at the top to 1 at
// the bottom; the shader puts them in place.
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_never, shadows_disabled;

#include "res://Rendering/water_tint.gdshaderinc"

// The water's color where it reaches the rim (a water terrain's, alpha 1; alpha 0 for the
// standard blue-green), and the height of the rim's top edge, in globe radii.
uniform vec4 tint : source_color = vec4(0.0);
uniform float top = 0.04;

const float RIM_RADIUS = PI;      // FlatDisc.Radius
const float FALL = 0.8;           // How far the water falls before it's gone, in globe radii
const float THROW = 0.06;         // How far out it's thrown by the bottom

varying float fallen;

// Smooth noise from 0 to 1 over a whole-number lattice in two dimensions.
float hash2(vec2 cell) {
	return fract(sin(dot(cell, vec2(127.1, 311.7))) * 43758.5453);
}

float noise2(vec2 p) {
	vec2 cell = floor(p);
	vec2 f = p - cell;
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash2(cell), hash2(cell + vec2(1.0, 0.0)), f.x),
		mix(hash2(cell + vec2(0.0, 1.0)), hash2(cell + vec2(1.0, 1.0)), f.x), f.y);
}

void vertex() {
	fallen = UV.y;
	float out_from_axis = RIM_RADIUS + 0.002 + THROW * sqrt(fallen);
	vec2 around = normalize(VERTEX.xz);
	VERTEX = vec3(around.x * out_from_axis, top - fallen * FALL, around.y * out_from_axis);
	NORMAL = vec3(around.x, 0.0, around.y);
}

void fragment() {
	vec3 shallow, deep;
	water_shades(tint, shallow, deep);

	// Streaks running down, many around the rim, sliding down over time.
	float streaks = noise2(vec2(UV.x * 2400.0, (fallen - TIME * 0.02) * 40.0));
	streaks = 0.55 + 0.45 * streaks;

	// White where it tips over the edge, water as it falls, then mist that thins away.
	float mist = smoothstep(0.08, 0.6, fallen);
	vec3 color = mix(shallow * 1.2, vec3(0.85, 0.9, 0.92), mist);
	color = mix(vec3(0.95), color, smoothstep(0.0, 0.015, fallen));
	ALBEDO = color;
	ROUGHNESS = 0.35;
	ALPHA = mix(0.9, 0.55, mist) * streaks * (1.0 - smoothstep(0.45, 1.0, fallen));
}
