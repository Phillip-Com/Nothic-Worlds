// Planet surface. Computes latitude/longitude from each point's direction instead of using the
// mesh's texture coordinates. This avoids seams and pole pinching, and keeps working when the
// sphere is replaced by a different planet mesh later.
//
// Axis convention must match NothicWorlds.Core.Geometry.SphericalCoordinates:
// +Y is north, longitude 0 faces +Z, longitude 90 east faces +X.
shader_type spatial;

// Map image: its width always spans longitude -180 (left) to 180 (right). How its height maps
// to latitude depends on `projection`. Clamped rather than repeating: repeating would blend the
// bottom edge into the top when a flat map's edge is stretched over the poles.
uniform sampler2D surface_map : source_color, filter_linear_mipmap, repeat_disable;
uniform bool has_map = false;
uniform bool show_grid = true;

// Must match NothicWorlds.Core.Maps.MapProjection:
// 0 = Equirectangular ("Globe map"), 1 = Mercator ("Flat map").
uniform int projection = 1;
// Map image width divided by height (used by the Mercator projection).
uniform float map_aspect = 2.0;

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

// Vertical position in the map image (0 = top, 1 = bottom). Same math as
// NothicWorlds.Core.Maps.MapProjections.ToImagePosition. Values beyond the map are clamped
// to its edge, which stretches the edge's pixels over the poles.
float map_v(float latitude) {
	if (projection == 1) {
		// Mercator grows without limit toward the poles; anything this far is outside the map.
		float lat = radians(clamp(latitude, -89.9, 89.9));
		float y = log(tan(PI / 4.0 + lat / 2.0));
		return clamp(0.5 - y / (2.0 * PI) * map_aspect, 0.0, 1.0);
	}
	return 0.5 - latitude / 180.0;
}

vec3 sample_map(float latitude, float longitude) {
	vec2 uv = vec2(longitude / 360.0 + 0.5, map_v(latitude));

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
