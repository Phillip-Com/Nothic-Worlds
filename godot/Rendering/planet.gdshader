// Planet surface. Computes latitude/longitude from each point's direction instead of using the
// mesh's texture coordinates. This avoids seams and pole pinching, and keeps working when the
// sphere is replaced by a different planet mesh later.
//
// Axis convention must match NothicWorlds.Core.Geometry.SphericalCoordinates:
// +Y is north, longitude 0 faces +Z, longitude 90 east faces +X.
shader_type spatial;

// Equirectangular (2:1) map: left edge is longitude -180, top edge is the north pole.
uniform sampler2D surface_map : source_color, filter_linear_mipmap, repeat_enable;
uniform bool has_map = false;
uniform bool show_grid = true;

uniform vec3 base_color : source_color = vec3(0.13, 0.27, 0.45);
uniform vec3 grid_color : source_color = vec3(0.75, 0.8, 0.9);
uniform vec3 highlight_color : source_color = vec3(1.0, 0.8, 0.35);
uniform float grid_spacing_degrees = 15.0;
uniform float grid_line_pixels = 1.2;

varying vec3 local_direction;

void vertex() {
	local_direction = VERTEX;
}

// 1.0 on a line through multiples of `spacing`, fading to 0.0 over `pixels` screen pixels.
float grid_line(float value, float spacing, float pixels) {
	float scaled = value / spacing;
	float distance_to_line = abs(fract(scaled - 0.5) - 0.5);
	return 1.0 - clamp(distance_to_line / (fwidth(scaled) * pixels), 0.0, 1.0);
}

vec3 sample_map(float latitude, float longitude) {
	vec2 uv = vec2(longitude / 360.0 + 0.5, 0.5 - latitude / 180.0);

	// At the 180° line, u jumps from 1 back to 0. The GPU sees that jump as a huge change and
	// picks the blurriest mipmap for those pixels, which shows up as a visible seam. A second
	// copy of u has its jump on the opposite side of the globe instead (at longitude 0). Use
	// whichever copy changes more smoothly to choose the mipmap level.
	vec2 dx = dFdx(uv);
	vec2 dy = dFdy(uv);
	float u_shifted = fract(uv.x + 0.5);
	float dx_shifted = dFdx(u_shifted);
	float dy_shifted = dFdy(u_shifted);
	if (abs(dx_shifted) + abs(dy_shifted) < abs(dx.x) + abs(dy.x)) {
		dx.x = dx_shifted;
		dy.x = dy_shifted;
	}

	return textureGrad(surface_map, uv, dx, dy).rgb;
}

void fragment() {
	vec3 direction = normalize(local_direction);
	float latitude = degrees(asin(clamp(direction.y, -1.0, 1.0)));
	float longitude = degrees(atan(direction.x, direction.z));

	vec3 color = has_map ? sample_map(latitude, longitude) : base_color;

	if (show_grid) {
		float grid = max(
			grid_line(latitude, grid_spacing_degrees, grid_line_pixels),
			grid_line(longitude, grid_spacing_degrees, grid_line_pixels));

		// The equator and longitude 0 stand out so the user can tell which way is which.
		float highlight = max(
			grid_line(latitude, 360.0, grid_line_pixels * 1.5),
			grid_line(longitude, 360.0, grid_line_pixels * 1.5));

		color = mix(color, grid_color, grid * 0.6);
		color = mix(color, highlight_color, highlight);
	}

	ALBEDO = color;
	ROUGHNESS = 0.9;
}
