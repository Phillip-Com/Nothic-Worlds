// The world's designed night sky (VISION.md REN-07): its fixed stars and constellation lines,
// from images StarSky.cs makes. Shared by the sky behind the system (nebula_sky.gdshader) and
// the sky seen from a world's surface (surface_sky.gdshader).
//
// The stars sit in the cells of a cube's six faces, one star at most per cell. Finding a
// direction's face and cell must match Core's StarField.CellOf exactly; keep the two in step.

uniform bool has_star_field = false;
uniform bool show_constellations = true;
// One texel per cell, six faces side by side: where the star is across and down its cell, its
// brightness (0 for none), and its temperature (color).
uniform sampler2D star_cells : filter_nearest, repeat_disable;
// How much constellation line covers each spot, laid out the same way at a finer size.
uniform sampler2D star_lines : filter_linear, repeat_disable;

const float STAR_FACE_CELLS = 128.0;

// Which cube face a direction meets (x: 0 to 5 for +X, -X, +Y, -Y, +Z, -Z) and where on it
// (y, z: -1 to 1).
vec3 star_face(vec3 d) {
	vec3 m = abs(d);
	if (m.x >= m.y && m.x >= m.z) {
		return d.x > 0.0 ? vec3(0.0, -d.z / m.x, d.y / m.x) : vec3(1.0, d.z / m.x, d.y / m.x);
	}
	if (m.y >= m.z) {
		return d.y > 0.0 ? vec3(2.0, d.x / m.y, -d.z / m.y) : vec3(3.0, d.x / m.y, d.z / m.y);
	}
	return d.z > 0.0 ? vec3(4.0, d.x / m.z, d.y / m.z) : vec3(5.0, -d.x / m.z, d.y / m.z);
}

// The direction through point (a, b) of a face (the reverse of star_face).
vec3 star_face_direction(int face, vec2 ab) {
	vec3 d = face == 0 ? vec3(1.0, ab.y, -ab.x)
		: face == 1 ? vec3(-1.0, ab.y, ab.x)
		: face == 2 ? vec3(ab.x, 1.0, -ab.y)
		: face == 3 ? vec3(ab.x, -1.0, ab.y)
		: face == 4 ? vec3(ab.x, ab.y, 1.0)
		: vec3(-ab.x, ab.y, -1.0);
	return normalize(d);
}

// A star's color from its temperature: red, through white, to blue (StarField.ColorOf).
vec3 star_tint(float t) {
	return t < 0.5
		? vec3(1.0, 0.72 + 0.56 * t, 0.5 + t)
		: vec3(1.0 - 0.7 * (t - 0.5), 1.0 - 0.3 * (t - 0.5), 1.0);
}

// The light of the stars and constellation lines in a direction (a unit vector in the
// system's frame). `pixel_angle` is how much sky a screen pixel covers (radians), so stars
// stay about a pixel or two across at any zoom.
vec3 designed_stars(vec3 direction, float pixel_angle) {
	if (!has_star_field) {
		return vec3(0.0);
	}

	vec3 at = star_face(direction);
	int face = int(at.x);
	vec2 cell = clamp(floor((at.yz + 1.0) * 0.5 * STAR_FACE_CELLS), 0.0, STAR_FACE_CELLS - 1.0);
	vec4 star = texelFetch(star_cells, ivec2(face * int(STAR_FACE_CELLS) + int(cell.x),
		int(cell.y)), 0);
	vec3 light = vec3(0.0);
	if (star.b > 0.0) {
		vec2 ab = (cell + star.rg) / STAR_FACE_CELLS * 2.0 - 1.0;
		float away = length(star_face_direction(face, ab) - direction);
		float size = max(pixel_angle, 1e-5) * (0.9 + 1.2 * star.b);
		light = star_tint(star.a) * (0.4 + 6.0 * star.b * star.b)
			* (1.0 - smoothstep(0.0, size, away));
	}

	if (show_constellations) {
		vec2 uv = vec2((float(face) + (at.y + 1.0) * 0.5) / 6.0, (at.z + 1.0) * 0.5);
		light += vec3(0.35, 0.5, 0.8) * 0.35 * texture(star_lines, uv).r;
	}
	return light;
}
