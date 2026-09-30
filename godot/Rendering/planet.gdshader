// Planet surface. Computes latitude/longitude from each point's direction instead of using the
// mesh's texture coordinates. This avoids seams and pole pinching, and keeps working when the
// sphere is replaced by a different planet mesh later.
//
// Axis convention must match NothicWorlds.Core.Geometry.SphericalCoordinates:
// +Y is north, longitude 0 faces +Z, longitude 90 east faces +X.
shader_type spatial;

// Map types. Must match NothicWorlds.Core.Maps.MapProjection, and the math below must match
// NothicWorlds.Core.Maps.MapProjections.ToImagePosition (which has the tests).
const int EQUIRECTANGULAR = 0;  // "Globe map"
const int MERCATOR = 1;         // "Flat map"
const int ROBINSON = 2;
const int WINKEL_TRIPEL = 3;
const int MOLLWEIDE = 4;
const int GALL_PETERS = 5;
const int POLAR = 6;
const int TWO_HEMISPHERES = 7;

const float HALF_PI = PI / 2.0;
const float SQRT2 = 1.41421356;

// Robinson's defining table at every 5° of latitude (0° to 90°).
const float ROBINSON_LENGTH[19] = {
	1.0000, 0.9986, 0.9954, 0.9900, 0.9822, 0.9730, 0.9600, 0.9427, 0.9216, 0.8962,
	0.8679, 0.8350, 0.7986, 0.7597, 0.7186, 0.6732, 0.6213, 0.5722, 0.5322 };
const float ROBINSON_HEIGHT[19] = {
	0.0000, 0.0620, 0.1240, 0.1860, 0.2480, 0.3100, 0.3720, 0.4340, 0.4958, 0.5571,
	0.6176, 0.6769, 0.7346, 0.7903, 0.8435, 0.8936, 0.9394, 0.9761, 1.0000 };

// Map image. Clamped rather than repeating, so edges never bleed across the poles.
uniform sampler2D surface_map : source_color, filter_linear_mipmap, repeat_disable;
uniform bool has_map = false;
uniform bool show_grid = true;
uniform int projection = 1;
// Map image width divided by height (the Mercator projection's coverage depends on it).
uniform float map_aspect = 2.0;
// Where the map doesn't cover the globe (a flat map's polar caps, a polar map's southern
// hemisphere), the surface fades to this color over `fill_blend_degrees` of latitude.
uniform vec3 fill_color : source_color = vec3(0.9, 0.93, 0.96);
uniform float fill_blend_degrees = 3.0;

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

// ----- Projections: latitude/longitude (radians) to image position (0-1, top-left origin) -----
// Longitude may be beyond ±PI; sample_map() relies on that for seamless filtering.

// Scales projected x/y, spanning ±half_size, to fill the image.
vec2 fill_image(vec2 xy, vec2 half_size) {
	return vec2(0.5 + xy.x / (2.0 * half_size.x), 0.5 - xy.y / (2.0 * half_size.y));
}

vec2 project_mercator(float lat, float lon) {
	// Grows without limit toward the poles; anything this far is outside the map anyway.
	float limited = clamp(lat, -HALF_PI * 0.999, HALF_PI * 0.999);
	float y = log(tan(PI / 4.0 + limited / 2.0));
	return vec2(0.5 + lon / (2.0 * PI), 0.5 - y / (2.0 * PI) * map_aspect);
}

vec2 project_robinson(float lat, float lon) {
	float index = min(abs(degrees(lat)) / 5.0, 18.0);
	int lower = min(int(index), 17);
	float t = index - float(lower);
	float len = mix(ROBINSON_LENGTH[lower], ROBINSON_LENGTH[lower + 1], t);
	float height = mix(ROBINSON_HEIGHT[lower], ROBINSON_HEIGHT[lower + 1], t);
	return fill_image(vec2(0.8487 * len * lon, 1.3523 * height * sign(lat)),
		vec2(0.8487 * PI, 1.3523));
}

vec2 project_winkel_tripel(float lat, float lon) {
	float alpha = acos(clamp(cos(lat) * cos(lon / 2.0), -1.0, 1.0));
	float sinc = alpha < 1e-5 ? 1.0 : sin(alpha) / alpha;
	float x = 0.5 * (lon * (2.0 / PI) + 2.0 * cos(lat) * sin(lon / 2.0) / sinc);
	float y = 0.5 * (lat + sin(lat) / sinc);
	return fill_image(vec2(x, y), vec2(1.0 + HALF_PI, HALF_PI));
}

// Mollweide's helper angle θ: solves 2θ + sin(2θ) = π·sin(lat) with a fixed number of Newton
// steps. It depends only on latitude and is the expensive part, so sample_map() computes it once
// per pixel and passes it to every Mollweide projection call.
float mollweide_theta(float lat) {
	float double_theta = 2.0 * lat;
	if (abs(lat) < HALF_PI - 1e-4) {
		float target = PI * sin(lat);
		for (int i = 0; i < 12; i++) {
			double_theta -= (double_theta + sin(double_theta) - target)
				/ (1.0 + cos(double_theta));
		}
	}
	return double_theta / 2.0;
}

vec2 project_mollweide(float theta, float lon) {
	vec2 xy = vec2(2.0 * SQRT2 / PI * lon * cos(theta), SQRT2 * sin(theta));
	return fill_image(xy, vec2(2.0 * SQRT2, SQRT2));
}

vec2 project_gall_peters(float lat, float lon) {
	return fill_image(vec2(lon / SQRT2, SQRT2 * sin(lat)), vec2(PI / SQRT2, SQRT2));
}

vec2 project_polar(float lat, float lon) {
	// North pole at the center, equator at the rim, longitude 0 pointing down. The southern
	// hemisphere maps to the rim and is covered by the fill color.
	float radius = (HALF_PI - max(lat, 0.0)) / HALF_PI;
	return fill_image(vec2(radius * sin(lon), -radius * cos(lon)), vec2(1.0));
}

// One circle of the Two hemispheres layout: azimuthal equidistant around (0, center_lon),
// drawn in the half of the image centered at u = center_u.
vec2 project_hemisphere(float lat, float lon, float center_lon, float center_u) {
	float relative = lon - center_lon;
	float dist = acos(clamp(cos(lat) * cos(relative), -1.0, 1.0));
	float scale = dist < 1e-5 ? 1.0 : dist / sin(dist);
	vec2 xy = scale * vec2(cos(lat) * sin(relative), sin(lat)) / HALF_PI;
	return vec2(center_u + xy.x * 0.25, 0.5 - xy.y * 0.5);
}

// `theta` is only used by Mollweide (see mollweide_theta); other types ignore it.
vec2 project(float lat, float lon, float theta) {
	if (projection == MERCATOR) { return project_mercator(lat, lon); }
	if (projection == ROBINSON) { return project_robinson(lat, lon); }
	if (projection == WINKEL_TRIPEL) { return project_winkel_tripel(lat, lon); }
	if (projection == MOLLWEIDE) { return project_mollweide(theta, lon); }
	if (projection == GALL_PETERS) { return project_gall_peters(lat, lon); }
	if (projection == POLAR) { return project_polar(lat, lon); }
	return vec2(0.5 + lon / (2.0 * PI), 0.5 - lat / PI);  // Equirectangular
}

// How many degrees of latitude a point is beyond the map's coverage (0 or less = covered).
float degrees_outside_map(float lat) {
	if (projection == MERCATOR) {
		return abs(degrees(lat)) - degrees(atan(sinh(PI / map_aspect)));
	}
	if (projection == POLAR) {
		return -degrees(lat);
	}
	return -1.0;
}

// Texture filtering blends nearby pixels, and when zoomed out it blends a wide area. Right at
// a map's outline (a circle's rim, the curved sides of Robinson/Winkel/Mollweide) that would mix
// in the background outside the map, drawing a faint line along the seams. Pull the sample
// point inside the outline by the width being blended. Points away from the edges don't move.
vec2 keep_inside_circle(vec2 uv, vec2 center, vec2 radius, vec2 dx, vec2 dy) {
	vec2 offset = (uv - center) / radius;  // In units of the circle's radius
	float blend_width = 1.5 * max(length(dx / radius), length(dy / radius));
	float max_distance = max(1.0 - blend_width, 0.0);
	float distance = length(offset);
	if (distance > max_distance) {
		offset *= max_distance / distance;
	}
	return center + offset * radius;
}

vec2 keep_inside_map(vec2 uv, float lat, float theta, vec2 dx, vec2 dy) {
	if (projection == POLAR) {
		return keep_inside_circle(uv, vec2(0.5), vec2(0.5), dx, dy);
	}
	if (projection == TWO_HEMISPHERES) {
		vec2 center = vec2(uv.x < 0.5 ? 0.25 : 0.75, 0.5);
		return keep_inside_circle(uv, center, vec2(0.25, 0.5), dx, dy);
	}
	// The map's left and right outline at this latitude: where longitude ±180° lands.
	float blend_width = 1.5 * max(abs(dx.x), abs(dy.x));
	float left = project(lat, -PI, theta).x + blend_width;
	float right = project(lat, PI, theta).x - blend_width;
	uv.x = left < right ? clamp(uv.x, left, right) : 0.5 * (left + right);
	return uv;
}

vec3 sample_map(float lat, float lon) {
	// Where the map jumps from one image edge to another (the 180° line, or the split between
	// two circles), the GPU sees a huge change in position and picks the blurriest mipmap,
	// which shows up as a visible seam. So compute a second, continuous version across the
	// jump and use its rate of change to choose the mipmap level.
	vec2 uv;
	vec2 dx;
	vec2 dy;
	float theta = projection == MOLLWEIDE ? mollweide_theta(lat) : 0.0;
	if (projection == TWO_HEMISPHERES) {
		// Both circles' formulas are smooth across the split lines. Compute both and keep
		// the one for this point's hemisphere.
		vec2 west = project_hemisphere(lat, lon, -HALF_PI, 0.25);
		vec2 east = project_hemisphere(lat, lon, HALF_PI, 0.75);
		vec2 west_dx = dFdx(west);
		vec2 west_dy = dFdy(west);
		vec2 east_dx = dFdx(east);
		vec2 east_dy = dFdy(east);
		bool is_west = lon < 0.0;
		uv = is_west ? west : east;
		dx = is_west ? west_dx : east_dx;
		dy = is_west ? west_dy : east_dy;
	} else {
		// A copy whose longitude runs past 180° instead of wrapping to -180° (its own jump
		// is at longitude 0, on the other side of the globe). Use whichever copy changes
		// more smoothly here.
		uv = project(lat, lon, theta);
		vec2 continuous = project(lat, lon < 0.0 ? lon + 2.0 * PI : lon, theta);
		dx = dFdx(uv);
		dy = dFdy(uv);
		vec2 continuous_dx = dFdx(continuous);
		vec2 continuous_dy = dFdy(continuous);
		if (length(continuous_dx) + length(continuous_dy) < length(dx) + length(dy)) {
			dx = continuous_dx;
			dy = continuous_dy;
		}
	}

	uv = keep_inside_map(uv, lat, theta, dx, dy);
	vec3 map_color = textureGrad(surface_map, clamp(uv, 0.0, 1.0), dx, dy).rgb;
	float fill = smoothstep(0.0, fill_blend_degrees, degrees_outside_map(lat));
	return mix(map_color, fill_color, fill);
}

void fragment() {
	vec3 direction = normalize(local_direction);
	float latitude = degrees(asin(clamp(direction.y, -1.0, 1.0)));
	float longitude = degrees(atan(direction.x, direction.z));

	vec3 color = has_map ? sample_map(radians(latitude), radians(longitude)) : base_color;

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
