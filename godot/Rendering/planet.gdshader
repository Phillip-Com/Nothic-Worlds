// Planet surface. Computes latitude/longitude from each point's direction instead of using the
// mesh's texture coordinates. This avoids seams and pole pinching, and keeps working when the
// sphere is replaced by a different planet mesh later.
//
// Axis convention must match NothicWorlds.Core.Geometry.SphericalCoordinates:
// +Y is north, longitude 0 faces +Z, longitude 90 east faces +X.
shader_type spatial;

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

void fragment() {
	vec3 direction = normalize(local_direction);
	float latitude = degrees(asin(clamp(direction.y, -1.0, 1.0)));
	float longitude = degrees(atan(direction.x, direction.z));

	float grid = max(
		grid_line(latitude, grid_spacing_degrees, grid_line_pixels),
		grid_line(longitude, grid_spacing_degrees, grid_line_pixels));

	// The equator and longitude 0 stand out so the user can tell which way is which.
	float highlight = max(
		grid_line(latitude, 360.0, grid_line_pixels * 1.5),
		grid_line(longitude, 360.0, grid_line_pixels * 1.5));

	vec3 color = mix(base_color, grid_color, grid * 0.6);
	ALBEDO = mix(color, highlight_color, highlight);
	ROUGHNESS = 0.9;
}
