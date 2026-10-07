// Shared by the globe (planet_surface.gdshaderinc), the first-person water surface
// (water_surface.gdshader), and the rim's waterfall (rim_waterfall.gdshader): where a flat
// world's point stands on the globe, where a direction falls on the terrain grid, and what
// color water is over the terrain there (VISION.md BOD-09; owner's choice: water over a
// terrain type with the Water climate takes that type's color, and water over land a standard
// blue-green).

const int WATER_TINT_CELLS = 1024;  // Cells across a face, as TERRAIN_FACE_SIZE
const vec3 WATER_SHALLOW = vec3(0.13, 0.42, 0.47);
const vec3 WATER_DEEP = vec3(0.02, 0.09, 0.24);

// On a flat world's top face, distance from the center is the angle from the north pole (the
// disc's radius is PI), so a point maps back to the globe direction it stands for. Must match
// FlatDisc in Core.
vec3 flat_disc_direction(vec3 point) {
	float across = length(point.xz);
	if (across < 1e-6) {
		return vec3(0.0, 1.0, 0.0);
	}
	float from_pole = min(across, PI);
	vec2 sideways = point.xz / across * sin(from_pole);
	return vec3(sideways.x, cos(from_pole), sideways.y);
}

// Where a direction meets the cube: (across, down, face), with across and down from 0 to 1 over
// the face (left to right, top to bottom). See CubeSphere.CellAt, whose face axes are written
// out here as plain components (indexing arrays per pixel is slow on some graphics cards).
// `on_face` is the point on the flat cube face: (along "right", along "up").
vec3 terrain_face_position(vec3 d) {
	vec3 size = abs(d);
	float face;
	vec2 on_face;
	if (size.x >= size.y && size.x >= size.z) {
		face = d.x >= 0.0 ? 0.0 : 1.0;
		on_face = vec2(-d.z, d.y) / d.x * vec2(1.0, sign(d.x));
	} else if (size.y >= size.z) {
		face = d.y >= 0.0 ? 2.0 : 3.0;
		on_face = vec2(d.x, -d.z) / d.y * vec2(sign(d.y), 1.0);
	} else {
		face = d.z >= 0.0 ? 4.0 : 5.0;
		on_face = vec2(d.x, d.y) / d.z * vec2(1.0, sign(d.z));
	}

	vec2 spread = atan(on_face) * (4.0 / PI);  // Equal-angle spacing, -1 to 1
	return vec3((spread.x + 1.0) * 0.5, (1.0 - spread.y) * 0.5, face);
}

// The color of the water terrain painted around a direction, with alpha for how much of it is
// (0 over land or unpainted ground): `cells` holds each cell's terrain code, and `palette`
// each code's water color (alpha 0 for a type that isn't water). Blended between the four
// nearest cells' middles, so it changes smoothly where two kinds of water meet.
vec4 water_tint(sampler2DArray cells, sampler2D palette, vec3 direction) {
	vec3 position = terrain_face_position(direction);
	vec2 at = position.xy * float(WATER_TINT_CELLS) - 0.5;
	ivec2 base = ivec2(floor(at));
	vec2 f = at - vec2(base);
	vec4 sum = vec4(0.0);
	for (int i = 0; i < 4; i++) {
		ivec2 offset = ivec2(i % 2, i / 2);
		ivec2 cell = clamp(base + offset, ivec2(0), ivec2(WATER_TINT_CELLS - 1));
		float code = texelFetch(cells, ivec3(cell, int(position.z)), 0).r * 255.0;
		vec4 color = texelFetch(palette, ivec2(int(round(code)), 0), 0);
		float weight = (offset.x == 1 ? f.x : 1.0 - f.x) * (offset.y == 1 ? f.y : 1.0 - f.y);
		sum += vec4(color.rgb * color.a, color.a) * weight;
	}

	return sum.a > 0.0 ? vec4(sum.rgb / sum.a, sum.a) : vec4(0.0);
}

// The water's color in its shallows and in its depths for a tint from water_tint: the water
// terrain's own color, darker further down, or the standard blue-green over land.
void water_shades(vec4 tint, out vec3 shallow, out vec3 deep) {
	shallow = mix(WATER_SHALLOW, tint.rgb, tint.a);
	deep = mix(WATER_DEEP, tint.rgb * 0.4, tint.a);
}
