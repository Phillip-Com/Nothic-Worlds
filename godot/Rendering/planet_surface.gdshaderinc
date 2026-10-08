// The planet surface's shared code, for every visual style (VISION.md REN-05): each
// planet_*.gdshader sets its style and includes this. Realistic (planet.gdshader) uses
// Godot's own lighting; the others add their own (planet_style.gdshaderinc).
//
// Planet surface. Computes latitude/longitude from each point's direction instead of using the
// mesh's texture coordinates. This avoids seams and pole pinching, and keeps working when the
// sphere is replaced by a different planet mesh later.
//
// Axis convention must match NothicWorlds.Core.Geometry.SphericalCoordinates:
// +Y is north, longitude 0 faces +Z, longitude 90 east faces +X.

#include "res://Rendering/ring_bands.gdshaderinc"

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

// Grid calibration (VISION.md MAP-05): lookup tables from NothicWorlds.Core.Maps.MapCalibration.
// Row 0 of each 1-pixel-tall table holds the drawn latitude (or longitude) in degrees, for true
// latitudes -90..90 (or longitudes -180..180) evenly spaced across its width.
uniform bool has_calibration = false;
uniform sampler2D calibration_latitudes : filter_linear, repeat_disable;
uniform sampler2D calibration_longitudes : filter_linear, repeat_disable;

// Map pieces (VISION.md MAP-02): up to 32 flat images laid on the globe like stickers, drawn
// over the map in order. The math must match NothicWorlds.Core.Maps.PieceProjection (which has
// the tests); the per-piece values come from it via PlanetSurface.SetPieces.
uniform int piece_count = 0;
uniform sampler2D piece_textures[32] : source_color, filter_linear_mipmap, repeat_disable;
uniform vec3 piece_centers[32];
uniform vec3 piece_easts[32];
uniform vec3 piece_norths[32];
// x, y: cosine and sine of the clockwise rotation; z, w: 1 / width and 1 / height (radians).
uniform vec4 piece_frames[32];
// x: cosine of the piece's reach (quick skip for far-away pixels); y: radians per texel.
uniform vec2 piece_limits[32];

// Warped pieces (Edit Points): each one's reverse lookup (Core WarpLookup) is a 64 x 64 tile in
// this atlas, 8 tiles across and 4 down. Each cell holds the unwarped box position (rg) and
// whether the piece covers it (b). Read with texelFetch, so no filtering is needed.
uniform sampler2D piece_warp_atlas : repeat_disable, filter_nearest;
// Each piece's tile in the atlas, or -1 if it isn't warped.
uniform int piece_warp_tiles[32];
// The part of each piece's box its lookup covers: (min u, min v, max u, max v).
uniform vec4 piece_warp_rects[32];
const int WARP_SIZE = 64;
const int WARP_TILES_ACROSS = 8;

// Painted terrain (VISION.md BOD-05): one terrain code per cell of a cube-sphere grid, the six
// faces as layers. The math must match NothicWorlds.Core.Geometry.CubeSphere (which has the
// tests). The palette holds each code's color, with alpha 0 for unpainted and unknown codes.
uniform bool has_terrain = false;
uniform bool show_terrain = true;
uniform sampler2DArray terrain_cells : filter_nearest, repeat_disable;
uniform sampler2D terrain_palette : source_color, filter_nearest, repeat_disable;
// Each code's color as water (alpha 0 for a type whose climate isn't water); see
// water_tint.gdshaderinc.
uniform sampler2D water_palette : source_color, filter_nearest, repeat_disable;
// Seen from far away, cells get smaller than a pixel and would flicker. This copy holds each
// face's colors averaged over 4 x 4 cells (alpha = how much is painted, colors multiplied by
// it), with mipmaps, and takes over as cells shrink.
uniform sampler2DArray terrain_far : source_color, filter_linear_mipmap, repeat_disable;
// How strongly terrain covers a map (and its pieces), so the map shows through for tracing.
// Without a map, terrain is the surface itself, and what's not painted is drawn in
// `unpainted_color` (owner's choice), so it's easy to tell from painted Ocean.
uniform float terrain_over_map = 0.6;
uniform vec3 unpainted_color : source_color = vec3(0.55, 0.56, 0.58);
const int TERRAIN_FACE_SIZE = 1024;
const int TERRAIN_FAR_SIZE = 256;
// Sculpted heights (VISION.md BOD-04) on the same cube grid: each cell's height in meters (as
// half-precision floats, so the graphics card blends between cells, with smaller copies for
// far away). The globe is then the cube-sphere mesh, each vertex lifted by its height times
// relief_scale (meters to the unit sphere's radius, times the view's exaggeration), and each
// pixel shaded by the slope.
uniform bool has_heights = false;
uniform sampler2DArray heights : filter_linear_mipmap, repeat_disable;
uniform float relief_scale = 0.0;
// Map-style shading (owner's choice, a View option): relief lit from a fixed direction, the
// northwest 45° up as on printed maps, so it always shows; the sun still makes day and night.
uniform bool map_shading = false;
// True when the globe is carved by shapes (ShapedGlobe): its mesh comes already lifted (and
// flat-faced, from the carving), so the shader neither lifts it nor uses its faces' normals.
uniform bool lifted_on_cpu = false;
// Water (VISION.md BOD-09): wherever the ground is below water_level (meters), the globe is
// drawn as water, level with it. The first-person view draws its own water surface, so the
// ground around the eye shows the true bottom.
uniform bool has_water = false;
uniform float water_level = 0.0;
// Lakes (VISION.md BOD-11): each cell's lake surface in meters, held by a lake's cells and
// those around them (so its shore follows the ground between) and very low elsewhere. Not
// blended, so each lake keeps its own level; drawn as water wherever the ground is lower, as
// for water_level.
uniform bool has_lakes = false;
uniform sampler2DArray lake_levels : filter_nearest, repeat_disable;

// Around a first-person eye, the globe's own mesh isn't raised to the water or drawn as it:
// the ground and the water's surface there are drawn up close (FirstPersonGround, FlatPatch),
// and a globe at water level would hide the bottom. Within near_radius of near_center: an
// angle from a direction on a globe, a distance on a flat world's face (negative: off).
uniform vec3 near_center = vec3(0.0, 1.0, 0.0);
uniform float near_radius = -1.0;

// The softest edge between terrains up close, in cells (edges sharpen to about a pixel as
// cells get bigger on screen, and never go softer than this).
const float TERRAIN_EDGE_CELLS = 0.15;
// How a body without a map looks (VISION.md BOD-06): its color and a pattern drawn over it.
// Patterns must match NothicWorlds.Core.Model.SurfacePattern.
uniform vec3 base_color : source_color = vec3(0.13, 0.27, 0.45);
uniform int surface_pattern = 0;
uniform vec3 pattern_offset = vec3(0.0);
uniform sampler3D pattern_noise : filter_linear, repeat_enable;
const int PATTERN_NOISE_SIZE = 32;
const int PATTERN_ROCKY = 1;
const int PATTERN_BANDED = 2;
const int PATTERN_ICY = 3;
const int PATTERN_CLOUDY = 4;
uniform vec3 grid_color : source_color = vec3(0.75, 0.8, 0.9);
uniform vec3 highlight_color : source_color = vec3(1.0, 0.8, 0.35);
uniform float grid_spacing_degrees = 15.0;
uniform float grid_line_pixels = 1.2;

// A flat world (VISION.md BOD-02): the mesh is a disc, and each point stands for the globe
// direction it maps to (see flat_disc_direction). Its top face is lifted by the ground
// (BOD-10), never lower than FLAT_DEEPEST radii below the face, which keeps it just above the
// underside; its outermost ring, all of which stands for the south pole, is lifted by
// rim_lift, as the rim wall's top edge is (PlanetSurface.UpdateRim), so the two always meet.
uniform bool flat_disc = false;
uniform float rim_lift = 0.0;
const float FLAT_DEEPEST = -0.075;  // PlanetSurface.FlatDeepestLift
const float FLAT_HALF_THICKNESS = 0.04;  // FlatDisc.HalfThickness
const vec3 ROCK_COLOR = vec3(0.105, 0.089, 0.074);  // PlanetSurface's rock, in linear light

// The planet's rings (VISION.md BOD-03), for the shadow they cast: their radii (in planet
// radii), their pattern, and the plane they lie in (its normal, in the planet's own space).
uniform bool has_rings = false;
uniform float ring_inner = 1.25;
uniform float ring_outer = 2.3;
uniform float ring_seed = 0.0;
uniform vec3 ring_normal = vec3(0.0, 1.0, 0.0);

// The way to the star, in the planet's own space: for the rings' shadow, and for live weather,
// which fades by night.
uniform vec3 sun_direction = vec3(0.0, 1.0, 0.0);

varying vec3 local_direction;

// 1 on the ground around a first-person eye, which leaves the clouds and the grid to the sky
// (surface_sky.gdshader draws the clouds overhead; seen from below they aren't on the ground).
varying float eye_level_ground;

// Fine detail on that ground near the eye (VISION.md REN-06): its position from the point the
// ground is built around (in globe radii, precise near the eye), where that point is in the
// detail's noise (FirstPersonMode works it out in double precision, wrapped to the noise's
// repeat so the pattern stays put as the ground is rebuilt), and the globe's radius in meters.
varying vec3 ground_offset;
uniform vec3 ground_detail_origin = vec3(0.0);
uniform float ground_radius_meters = 6371000.0;

// The detail's coarsest noise is this many meters across.
const float GROUND_DETAIL_METERS = 4.0;

// Two turns for the detail's noise layers, each three times a rotation. Their whole-number
// entries keep the noise repeating as it must (see ground_detail_origin), which any other
// rotation would break, making the detail jump each time the ground is rebuilt.
const mat3 DETAIL_TURN_A = mat3(vec3(2.0, -2.0, 1.0), vec3(2.0, 1.0, -2.0), vec3(1.0, 2.0, 2.0));
const mat3 DETAIL_TURN_B = mat3(vec3(1.0, 2.0, 2.0), vec3(2.0, 1.0, -2.0), vec3(2.0, -2.0, 1.0));

// 1.0 on a line through multiples of `spacing`, fading to 0.0 over `pixels` screen pixels.
// `width` is how much `value` changes from pixel to pixel.
float grid_line(float value, float width, float spacing, float pixels) {
	float scaled = value / spacing;
	float distance_to_line = abs(fract(scaled - 0.5) - 0.5);
	return 1.0 - clamp(distance_to_line / (width / spacing * pixels), 0.0, 1.0);
}

// How much longitude (degrees) changes from pixel to pixel. Longitude jumps from 180 to −180
// at the date line, where the GPU's own measure would be huge (it drew a dotted line there), so
// it's also measured from 0 to 360, which jumps elsewhere, and the smaller taken.
float longitude_width(float longitude) {
	return min(fwidth(longitude), fwidth(mod(longitude + 360.0, 360.0)));
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

// Reads a calibration table at `fraction` (0..1 across its range), hitting texel centers so
// the ends map exactly to the first and last samples.
float read_table(sampler2D table, float fraction) {
	float width = float(textureSize(table, 0).x);
	return texture(table, vec2((fraction * (width - 1.0) + 0.5) / width, 0.5)).r;
}

// Takes the TRUE latitude/longitude (radians). With a calibration, first looks up where they're
// drawn in the map type's own grid; everything after works on those drawn values.
vec3 sample_map(float true_lat, float true_lon) {
	float lat = true_lat;
	float lon = true_lon;
	if (has_calibration) {
		lat = radians(read_table(calibration_latitudes, true_lat / PI + 0.5));
		// The table isn't wrapped (it stays smooth for blending); wrap into -180..180 here.
		float drawn = read_table(calibration_longitudes, true_lon / (2.0 * PI) + 0.5);
		lon = radians(mod(drawn + 180.0, 360.0) - 180.0);
	}

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

// Turns a position in a warped piece's box into the unwarped position drawn there. Returns false
// if the piece doesn't cover it. Must match WarpLookup.Sample in Core exactly.
bool unwarp(int piece, inout vec2 uv) {
	vec4 rect = piece_warp_rects[piece];
	vec2 cell = (uv - rect.xy) / (rect.zw - rect.xy) * float(WARP_SIZE) - 0.5;
	if (cell.x < -1.0 || cell.y < -1.0
			|| cell.x > float(WARP_SIZE) || cell.y > float(WARP_SIZE)) {
		return false;
	}

	ivec2 base = ivec2(floor(cell));
	vec2 f = cell - vec2(base);
	int tile = piece_warp_tiles[piece];
	ivec2 origin = ivec2(tile % WARP_TILES_ACROSS, tile / WARP_TILES_ACROSS) * WARP_SIZE;
	float covered = 0.0;
	vec2 sum = vec2(0.0);
	for (int dy = 0; dy <= 1; dy++) {
		for (int dx = 0; dx <= 1; dx++) {
			ivec2 at = clamp(base + ivec2(dx, dy), ivec2(0), ivec2(WARP_SIZE - 1));
			vec4 value = texelFetch(piece_warp_atlas, origin + at, 0);
			float weight = (dx == 0 ? 1.0 - f.x : f.x) * (dy == 0 ? 1.0 - f.y : f.y) * value.b;
			covered += weight;
			sum += weight * value.rg;
		}
	}

	if (covered < 0.5) {
		return false;
	}
	// The lookup reaches a little past the piece; cut its edge exactly where the box ends.
	uv = sum / covered;
	return uv.x >= 0.0 && uv.x <= 1.0 && uv.y >= 0.0 && uv.y <= 1.0;
}

// Draws each piece covering this point, in order. `pixel_angle` is how much of the globe (in
// radians) one screen pixel covers here; it picks the texture's detail level, since the usual
// automatic choice doesn't work inside a loop that skips pixels.
vec3 draw_pieces(vec3 color, vec3 direction, float pixel_angle) {
	for (int i = 0; i < piece_count; i++) {
		float cos_distance = dot(direction, piece_centers[i]);
		if (cos_distance < piece_limits[i].x) {
			continue;  // Too far from this piece's center to be part of it.
		}

		float east = dot(direction, piece_easts[i]);
		float north = dot(direction, piece_norths[i]);
		float sideways = sqrt(east * east + north * north);
		// atan of sine and cosine stays accurate near the center; acos alone doesn't.
		float distance = atan(sideways, cos_distance);
		float scale = sideways > 1e-9 ? distance / sideways : 1.0;
		float x = east * scale;
		float y = north * scale;

		vec4 frame = piece_frames[i];
		float right = x * frame.x - y * frame.y;
		float up = x * frame.y + y * frame.x;
		vec2 uv = vec2(0.5 + right * frame.z, 0.5 - up * frame.w);
		if (piece_warp_tiles[i] >= 0) {
			if (!unwarp(i, uv)) {
				continue;
			}
		} else if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) {
			continue;
		}

		float lod = max(log2(max(pixel_angle / piece_limits[i].y, 1e-6)), 0.0);
		vec4 texel = textureLod(piece_textures[i], uv, lod);
		color = mix(color, texel.rgb, texel.a);
	}
	return color;
}

#include "res://Rendering/water_tint.gdshaderinc"

// The reverse: the direction to a position on a face (positions a little past the edge give
// directions over the next face, which terrain_face_position then finds).
vec3 terrain_direction(int face, vec2 position) {
	vec2 flat_cube = tan((vec2(position.x, 1.0 - position.y) * 2.0 - 1.0) * (PI / 4.0));
	float a = flat_cube.x;  // Along the face's "right"
	float b = flat_cube.y;  // Along its "up"
	if (face == 0) { return vec3(1.0, b, -a); }
	if (face == 1) { return vec3(-1.0, b, a); }
	if (face == 2) { return vec3(a, 1.0, -b); }
	if (face == 3) { return vec3(a, -1.0, b); }
	if (face == 4) { return vec3(a, b, 1.0); }
	return vec3(-a, b, -1.0);
}

// The height where a direction meets the surface, in meters, blended between the four nearest
// cells' middles on its face (as HeightGrid.SampleAt in Core), or, at `lod` above 0, from a
// smaller copy averaging 2^lod cells across.
float height_at(vec3 direction, float lod) {
	vec3 position = terrain_face_position(direction);
	return textureLod(heights, position, lod).r;
}

// True for a point (a direction on a globe, a place on a flat world's face) near the eye.
bool near_eye(vec3 point) {
	if (near_radius <= 0.0) {
		return false;
	}

	return flat_disc
		? distance(point.xz, near_center.xz) < near_radius
		: dot(normalize(point), near_center) > cos(near_radius);
}

// The ground's height in meters (0 where nothing is sculpted).
float ground_height(vec3 direction, float lod) {
	return has_heights ? height_at(direction, lod) : 0.0;
}

// The water's surface in meters: the sea's or a lake's, whichever is higher (very low with
// neither).
float water_surface(vec3 direction) {
	float surface = has_water ? water_level : -1e9;
	if (has_lakes) {
		surface = max(surface,
			textureLod(lake_levels, terrain_face_position(direction), 0.0).r);
	}

	return surface;
}

// Which way the sculpted ground faces here, in the body's own space: the slopes found by
// comparing the heights a cell either side (or a pixel, once cells are smaller than pixels, so
// distant ground doesn't shimmer). Comparing both sides keeps the cells' edges from showing.
vec3 relief_normal(vec3 direction, float pixel_angle) {
	float cell = HALF_PI / float(TERRAIN_FACE_SIZE);
	float step = max(cell, pixel_angle);
	float lod = log2(step / cell);
	vec3 side = abs(direction.y) < 0.99 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0);
	vec3 first = normalize(cross(direction, side));
	vec3 second = cross(direction, first);
	float rise_first = height_at(normalize(direction + first * step), lod)
		- height_at(normalize(direction - first * step), lod);
	float rise_second = height_at(normalize(direction + second * step), lod)
		- height_at(normalize(direction - second * step), lod);
	return normalize(direction
		- (first * rise_first + second * rise_second) * (relief_scale / (2.0 * step)));
}

// The height at a point on a flat world's top face, in meters.
float flat_height(vec2 at, float lod) {
	return height_at(flat_disc_direction(vec3(at.x, 0.0, at.y)), lod);
}

// Which way the ground faces on a flat world's top face at a point `at` (x, z) on it, as
// relief_normal does on a globe: from the slopes across the disc, where a step of one unit is
// a radian of the globe, so a cell is as wide as on a globe.
vec3 flat_relief_normal(vec2 at, float pixel_angle) {
	float cell = HALF_PI / float(TERRAIN_FACE_SIZE);
	float step = max(cell, pixel_angle);
	float lod = log2(step / cell);
	float rise_x = flat_height(at + vec2(step, 0.0), lod) - flat_height(at - vec2(step, 0.0), lod);
	float rise_z = flat_height(at + vec2(0.0, step), lod) - flat_height(at - vec2(0.0, step), lod);
	vec2 slope = vec2(rise_x, rise_z) * (relief_scale / (2.0 * step));
	return normalize(vec3(-slope.x, 1.0, -slope.y));
}

// A cell's terrain code, and its color with alpha (0 where nothing is painted).
int terrain_code(ivec3 cell) {
	return int(round(texelFetch(terrain_cells, cell, 0).r * 255.0));
}

vec4 terrain_code_color(int code) {
	return texelFetch(terrain_palette, ivec2(code, 0), 0);
}

// Up close: the four cells around this point, with smooth, softly anti-aliased edges between
// terrains instead of square cells. Each terrain's share of the four (by distance) decides; the
// edge runs where the two biggest shares are equal, which gives rounded coastlines.
vec4 terrain_near(vec3 position, float cells_per_pixel) {
	int face = int(position.z);
	vec2 cells = position.xy * float(TERRAIN_FACE_SIZE) - 0.5;
	ivec2 base = ivec2(floor(cells));
	vec2 f = cells - vec2(base);
	bool inside_face = base.x >= 0 && base.y >= 0
		&& base.x < TERRAIN_FACE_SIZE - 1 && base.y < TERRAIN_FACE_SIZE - 1;

	int codes[4];
	float weights[4];
	for (int i = 0; i < 4; i++) {
		ivec2 corner = base + ivec2(i % 2, i / 2);
		ivec3 cell;
		if (inside_face) {
			cell = ivec3(corner, face);
		} else {
			// Along a face edge, a neighbor may lie on the next face: find it by direction.
			vec2 at = (vec2(corner) + 0.5) / float(TERRAIN_FACE_SIZE);
			vec3 next = terrain_face_position(terrain_direction(face, at));
			ivec2 index = ivec2(floor(next.xy * float(TERRAIN_FACE_SIZE)));
			cell = ivec3(clamp(index, ivec2(0), ivec2(TERRAIN_FACE_SIZE - 1)), int(next.z));
		}
		codes[i] = terrain_code(cell);
		weights[i] = (i % 2 == 0 ? 1.0 - f.x : f.x) * (i / 2 == 0 ? 1.0 - f.y : f.y);
	}

	// Inside one terrain (most of the globe), there's no edge to draw.
	if (codes[0] == codes[1] && codes[0] == codes[2] && codes[0] == codes[3]) {
		vec4 color = terrain_code_color(codes[0]);
		return vec4(color.rgb * color.a, color.a);
	}

	// Each corner's terrain's total share; keep the biggest and the biggest other one.
	int best = codes[0];
	float best_share = -1.0;
	int second = codes[0];
	float second_share = 0.0;
	for (int i = 0; i < 4; i++) {
		float share = 0.0;
		for (int j = 0; j < 4; j++) {
			share += codes[j] == codes[i] ? weights[j] : 0.0;
		}
		if (share > best_share) {
			if (codes[i] != best) {
				second = best;
				second_share = best_share;
			}
			best = codes[i];
			best_share = share;
		} else if (codes[i] != best && share > second_share) {
			second = codes[i];
			second_share = share;
		}
	}

	float edge = max(cells_per_pixel, TERRAIN_EDGE_CELLS);
	float blend = second == best ? 1.0 : smoothstep(-edge, edge, best_share - second_share);
	vec4 best_color = terrain_code_color(best);
	vec4 second_color = terrain_code_color(second);
	// Blended with alpha, so an edge against unpainted ground fades rather than darkens.
	vec4 near = mix(vec4(second_color.rgb * second_color.a, second_color.a),
		vec4(best_color.rgb * best_color.a, best_color.a), blend);
	return near;
}

// The painted terrain's color here, with its colors multiplied by alpha (alpha 0 where nothing
// is painted). `pixel_angle` is how much of the globe one screen pixel covers (radians).
vec4 terrain_color(vec3 direction, float pixel_angle) {
	vec3 position = terrain_face_position(direction);
	float cells_per_pixel = pixel_angle / (HALF_PI / float(TERRAIN_FACE_SIZE));

	// Cells bigger than about a pixel: draw them; smaller: the averaged copy; blend between.
	float far_share = smoothstep(0.6, 1.5, cells_per_pixel);
	vec4 color = vec4(0.0);
	if (far_share < 1.0) {
		color = terrain_near(position, cells_per_pixel);
	}
	if (far_share > 0.0) {
		float far_texels = cells_per_pixel * float(TERRAIN_FAR_SIZE) / float(TERRAIN_FACE_SIZE);
		float lod = max(log2(max(far_texels, 1e-6)), 0.0);
		color = mix(color, textureLod(terrain_far, position, lod), far_share);
	}
	return color;
}

// ----- Surface patterns: noise on the globe's directions, so there are no seams -----
// The noise comes from a small repeating 3D texture of random values (PatternNoise.cs), read
// with smooth filtering: one texture read per layer of detail, which integrated graphics
// handle far better than working the noise out in every pixel.

float hash(vec3 p) {
	p = fract(p * 0.1031);
	p += dot(p, p.zyx + 31.32);
	return fract((p.x + p.y) * p.z);
}

// Smooth noise from 0 to 1, varying over about one unit.
float value_noise(vec3 p) {
	return texture(pattern_noise, p / float(PATTERN_NOISE_SIZE)).r;
}

// Noise with finer detail layered on, from about 0 to 1.
float fbm(vec3 p) {
	float total = 0.0;
	float amplitude = 0.5;
	for (int i = 0; i < 4; i++) {
		total += amplitude * value_noise(p);
		p = p * 2.03 + vec3(1.7, 9.2, 3.1);
		amplitude *= 0.5;
	}
	return total / 0.9375;
}

// How deep into a crater a point is: 1 in the middle of a crater's floor, a negative dip just
// inside its rim, and 0 outside. Some cells of a 3D grid hold one crater, small enough to sit
// wholly inside its cell, so only the point's own cell needs checking.
float craters(vec3 p) {
	vec3 cell = floor(p);
	float size = hash(cell + 7.0);
	if (size < 0.45) {
		return 0.0;
	}
	vec3 centre = cell + 0.35 + 0.3 * vec3(hash(cell), hash(cell + 3.0), hash(cell + 5.0));
	float radius = 0.12 + 0.2 * (size - 0.45);
	float d = length(p - centre) / radius;
	float floor_depth = 1.0 - smoothstep(0.6, 0.9, d);
	float rim = smoothstep(0.8, 0.95, d) * (1.0 - smoothstep(0.95, 1.15, d));
	return floor_depth - rim * 0.8;
}

// The surface's own look where there's no map: the color with its pattern. Each body's
// pattern is shifted by its own offset, so two rocky moons don't look the same.
vec3 surface_look(vec3 unit_direction) {
	vec3 direction = unit_direction + pattern_offset;
	vec3 color = base_color;
	if (surface_pattern == PATTERN_ROCKY) {
		color *= 0.75 + 0.45 * fbm(direction * 6.0);
		float crater = min(craters(direction * 9.0), 1.0) + craters(direction * 17.0 + 40.0);
		color *= 1.0 - 0.25 * crater + 0.3 * max(-crater, 0.0);
	} else if (surface_pattern == PATTERN_BANDED) {
		float wobble = fbm(direction * 3.0) * 1.6;
		float bands = sin(direction.y * 18.0 + wobble) * 0.6
			+ sin(direction.y * 41.0 + wobble * 2.0) * 0.4;
		color *= 0.85 + 0.3 * bands;
	} else if (surface_pattern == PATTERN_ICY) {
		color = mix(color, vec3(0.95, 0.97, 1.0), 0.45);
		float crack = 1.0 - smoothstep(0.0, 0.035, abs(fbm(direction * 5.0) - 0.5));
		color *= 1.0 - 0.45 * crack;
	} else if (surface_pattern == PATTERN_CLOUDY) {
		vec3 swirl = direction * 3.0 + vec3(fbm(direction * 2.0), value_noise(direction * 2.0 + 4.0),
			0.0) * 1.5;
		float clouds = smoothstep(0.45, 0.72, fbm(swirl));
		color = mix(color, vec3(0.93, 0.94, 0.96), clouds);
	}
	return color;
}

// How bright map-style shading makes ground facing `normal` at `direction`: 1 on level ground,
// brighter on slopes facing the northwest light, darker on those facing away. Like printed
// relief maps, it steepens the slopes for shading (MAP_SHADE_STEEPENING times), so gentle
// relief still shows.
const float MAP_SHADE_STEEPENING = 5.0;

float map_shade(vec3 normal, vec3 direction) {
	normal = normalize(direction + (normal - direction) * MAP_SHADE_STEEPENING);
	vec3 toward_pole = vec3(0.0, 1.0, 0.0) - direction * direction.y;
	vec3 north = length(toward_pole) > 1e-4 ? normalize(toward_pole) : vec3(0.0, 0.0, 1.0);
	vec3 east = cross(north, direction);
	vec3 light = normalize(direction + north - east);  // From the northwest, 45° up
	// Softened, as printed relief is: steep slopes darken without going black.
	float facing = dot(normal, light) / dot(direction, light);
	return clamp(1.0 + 0.6 * (facing - 1.0), 0.4, 1.5);
}

#include "res://Rendering/planet_weather.gdshaderinc"

#ifdef STYLED
#include "res://Rendering/planet_style.gdshaderinc"
#endif

void vertex() {
	// The ground around a first-person eye (VISION.md REN-06, FirstPersonGround) is built
	// around the eye, not the globe's middle, so its positions keep their precision; it brings
	// each point's direction from the globe's middle along (CUSTOM0, w = 1) and is lifted
	// already. Other meshes have no CUSTOM0 (it reads as 0).
	eye_level_ground = CUSTOM0.w > 0.5 ? 1.0 : 0.0;
	ground_offset = VERTEX;
	if (CUSTOM0.w > 0.5) {
		local_direction = CUSTOM0.xyz;
	} else {
		local_direction = VERTEX;
		if ((has_heights || has_water || has_lakes) && !lifted_on_cpu) {
			// Lifted to the ground, or to the water over it.
			vec3 direction = flat_disc ? flat_disc_direction(VERTEX) : normalize(VERTEX);
			float height = ground_height(direction, 0.0);
			height = (has_water || has_lakes) && !near_eye(VERTEX)
				? max(height, water_surface(direction))
				: height;
			if (!flat_disc) {
				VERTEX = direction * (1.0 + height * relief_scale);
			} else if (length(VERTEX.xz) < PI - 1e-4) {
				VERTEX.y += max(height * relief_scale, FLAT_DEEPEST);
			} else {
				VERTEX.y += rim_lift;
			}
		}
	}
}

// How much of each kind of ground a color looks like: grass, sand, snow, and water (rock and
// soil are what's left). Taken from the ground's color, so it works for painted terrain and for
// maps alike.
vec4 ground_kinds(vec3 color) {
	float low = min(color.r, min(color.g, color.b));
	float grass = smoothstep(0.02, 0.1, color.g - max(color.r, color.b));
	float water = smoothstep(0.03, 0.12, color.b - max(color.r, color.g));
	float snow = smoothstep(0.6, 0.8, low);
	float sand = (1.0 - snow) * smoothstep(0.05, 0.2, (color.r + color.g) * 0.5 - color.b)
		* smoothstep(0.35, 0.55, color.r);
	return vec4(grass, sand, snow, water);
}

// Smooth noise like value_noise, but blended here at full precision with a gentle curve:
// the texture unit blends in steps of about 1/256, which seen up close (one noise cell across
// much of the screen) showed as rings and bands, worst in the bumps' shading.
float fine_noise(vec3 p) {
	vec3 cell = floor(p);
	vec3 f = p - cell;
	f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
	ivec3 c = ivec3(mod(cell, float(PATTERN_NOISE_SIZE)));
	ivec3 n = (c + 1) % PATTERN_NOISE_SIZE;
	float c000 = texelFetch(pattern_noise, ivec3(c.x, c.y, c.z), 0).r;
	float c100 = texelFetch(pattern_noise, ivec3(n.x, c.y, c.z), 0).r;
	float c010 = texelFetch(pattern_noise, ivec3(c.x, n.y, c.z), 0).r;
	float c110 = texelFetch(pattern_noise, ivec3(n.x, n.y, c.z), 0).r;
	float c001 = texelFetch(pattern_noise, ivec3(c.x, c.y, n.z), 0).r;
	float c101 = texelFetch(pattern_noise, ivec3(n.x, c.y, n.z), 0).r;
	float c011 = texelFetch(pattern_noise, ivec3(c.x, n.y, n.z), 0).r;
	float c111 = texelFetch(pattern_noise, ivec3(n.x, n.y, n.z), 0).r;
	return mix(mix(mix(c000, c100, f.x), mix(c010, c110, f.x), f.y),
		mix(mix(c001, c101, f.x), mix(c011, c111, f.x), f.y), f.z);
}

// A noise layer's value, faded to its middle where it's finer than a pixel covers (`footprint`:
// noise units per pixel), so it never shimmers.
float detail_layer(vec3 q, float footprint) {
	float shown = 1.0 - smoothstep(0.25, 0.6, footprint);
	return shown > 0.0 ? mix(0.5, fine_noise(q), shown) : 0.5;
}

// Fine texture and small bumps underfoot (VISION.md REN-06, owner's choice), matched to the
// kind of ground: noise about 4 m, 1 m, and 25 cm across, each fading out once a pixel covers
// more than it (`meters_per_pixel`, worked out from distance and angle so it's smooth across
// the mesh's triangles), so the detail is sharp by the feet and gone where it would shimmer.
// Changes the color and returns the bump height in meters (the caller shades by it).
float ground_detail(inout vec3 color, float meters_per_pixel) {
	vec3 q = ground_offset * ground_radius_meters / GROUND_DETAIL_METERS + ground_detail_origin;
	float footprint = meters_per_pixel / GROUND_DETAIL_METERS;
	if (footprint > 0.6) {
		return 0.0;
	}

	// The finer layers are turned different ways, so the noise's square lattice doesn't line
	// up into a visible grid (each turn is three times a rotation, about 3 and 15 times finer).
	float broad = detail_layer(q, footprint);
	float medium = detail_layer(DETAIL_TURN_A * q, footprint * 3.0);
	float grain = detail_layer(DETAIL_TURN_B * q * 5.0 + 5.7, footprint * 15.0);
	vec4 kinds = ground_kinds(color);
	float rock = clamp(1.0 - kinds.x - kinds.y - kinds.z - kinds.w, 0.0, 1.0);

	// Tufts and blades in grass, a fine grain in sand, gentle drifts in snow, mottling on
	// rock and soil, and almost nothing on water.
	float shade = kinds.x * (0.4 * (medium - 0.5) + 0.6 * (grain - 0.5))
		+ kinds.y * (0.28 * (grain - 0.5) + 0.12 * (broad - 0.5))
		+ kinds.z * 0.09 * (broad - 0.5)
		+ rock * (0.4 * (broad - 0.5) + 0.35 * (medium - 0.5) + 0.35 * (grain - 0.5))
		+ kinds.w * 0.04 * (medium - 0.5);
	color *= 1.0 + shade;

	float bumps = kinds.x * 0.14 + kinds.y * 0.08 + kinds.z * 0.16 + rock * 0.45;
	return bumps * (0.6 * broad + 0.3 * medium + 0.1 * grain);
}

// Water `depth` meters deep over ground of color `bottom`, in the shades of `tint` (see
// water_shades): clear in the shallows, darker further down, the bottom showing through only
// where it's very shallow.
vec3 water_color(vec3 bottom, float depth, vec4 tint) {
	vec3 shallow, deep;
	water_shades(tint, shallow, deep);
	vec3 water = mix(shallow, deep, 1.0 - exp(-depth / 700.0));
	return mix(bottom, water, clamp(0.55 + depth / 60.0, 0.0, 1.0));
}

// Ripples of light on the bottom under water (VISION.md BOD-09), `depth` meters down: a web of
// bright lines where two drifting layers of noise cross their middles, like sunlight focused
// by the waves above. They fade with depth, and where a pixel covers more than they do
// (`meters_per_pixel`), so they never shimmer.
float caustics(float depth, float meters_per_pixel) {
	float footprint = meters_per_pixel / GROUND_DETAIL_METERS * 3.0;
	if (footprint > 0.6) {
		return 0.0;
	}

	vec3 q = ground_offset * ground_radius_meters / GROUND_DETAIL_METERS + ground_detail_origin;
	float first = fine_noise(q + vec3(0.0, TIME * 0.3, TIME * 0.2));
	float second = fine_noise(DETAIL_TURN_A * q + vec3(TIME * 0.25, 0.0, -TIME * 0.2));
	float lines = max(1.0 - abs(2.0 * first - 1.0), 1.0 - abs(2.0 * second - 1.0));
	return pow(lines, 12.0) * exp(-depth / 12.0) * (1.0 - smoothstep(0.25, 0.6, footprint));
}

// A normal (in the globe's own space) tilted by a height in meters that changes across the
// ground, from how it changes between neighboring pixels (bump mapping by screen
// derivatives). Positions are the ground's own, in meters, which keep their precision here.
vec3 bumped(vec3 normal, float height) {
	vec3 position = ground_offset * ground_radius_meters;
	vec3 across = dFdx(position);
	vec3 down = dFdy(position);
	vec3 r1 = cross(down, normal);
	vec3 r2 = cross(normal, across);
	float det = dot(across, r1);
	vec3 slope = sign(det) * (dFdx(height) * r1 + dFdy(height) * r2);
	return normalize(abs(det) * normal - slope);
}

void fragment() {
	vec3 direction = flat_disc
		? flat_disc_direction(local_direction)
		: normalize(local_direction);
	float latitude = degrees(asin(clamp(direction.y, -1.0, 1.0)));
	float longitude = degrees(atan(direction.x, direction.z));

	bool terrain_shown = has_terrain && show_terrain;
	bool over_map = has_map || piece_count > 0;
	vec3 color = has_map ? sample_map(radians(latitude), radians(longitude))
		: terrain_shown ? unpainted_color : surface_look(direction);
	float pixel_angle = length(fwidth(direction));
	float latitude_step = fwidth(latitude);
	float longitude_step = longitude_width(longitude);
	color = draw_pieces(color, direction, pixel_angle);
	if (terrain_shown) {
		// Colors come multiplied by alpha: "over" blending, weakened over a map.
		vec4 terrain = terrain_color(direction, pixel_angle);
		float strength = over_map ? terrain_over_map : 1.0;
		color = color * (1.0 - terrain.a * strength) + terrain.rgb * strength;
	}

	// Water over the ground below its level, its shore smoothed over about a pixel. Not on
	// the first-person ground, which is the true bottom under its own water surface.
	float wet = 0.0;
	if ((has_water || has_lakes) && eye_level_ground < 0.5 && !near_eye(local_direction)) {
		float depth = water_surface(direction) - ground_height(direction, 0.0);
		wet = clamp(depth / max(fwidth(depth), 1e-3) + 0.5, 0.0, 1.0);
		vec4 tint = has_terrain
			? water_tint(terrain_cells, water_palette, direction)
			: vec4(0.0);
		color = mix(color, water_color(color, max(depth, 0.0), tint), wet);
	}

	// The rings' shadow: where the line from here toward the star crosses them. (A flat
	// world's face is left unshadowed, as for eclipses.)
	if (has_rings && !flat_disc) {
		float facing = dot(sun_direction, ring_normal);
		float ahead = abs(facing) > 1e-4 ? -dot(direction, ring_normal) / facing : -1.0;
		if (ahead > 0.0) {
			vec3 hit = direction + sun_direction * ahead;
			float radius = length(hit - ring_normal * dot(hit, ring_normal));
			color *= 1.0 - 0.75 * ring_density(
				(radius - ring_inner) / (ring_outer - ring_inner), ring_seed);
		}
	}

	vec3 weather_glow = vec3(0.0);
	if (has_weather && eye_level_ground < 0.5) {
		color = draw_weather(color, direction, radians(latitude), radians(longitude),
			pixel_angle, weather_glow);
	}

	if (show_grid && eye_level_ground < 0.5) {
		float grid = max(
			grid_line(latitude, latitude_step, grid_spacing_degrees, grid_line_pixels),
			grid_line(longitude, longitude_step, grid_spacing_degrees, grid_line_pixels));

		// The equator and longitude 0 stand out so the user can tell which way is which.
		float highlight = max(
			grid_line(latitude, latitude_step, 360.0, grid_line_pixels * 1.5),
			grid_line(longitude, longitude_step, 360.0, grid_line_pixels * 1.5));

		color = mix(color, grid_color, grid * 0.6);
		color = mix(color, highlight_color, highlight);
	}

	// The bump underfoot, in meters (first-person ground only).
	float bump_meters = 0.0;
	if (eye_level_ground > 0.5) {
		// How much ground a pixel covers: its width at that distance, stretched where the
		// ground is seen edge-on.
		float meters_away = length(VERTEX) / length(MODEL_MATRIX[0].xyz) * ground_radius_meters;
		float facing = max(abs(dot(normalize(VERTEX), normalize(NORMAL))), 0.1);
		float pixel_size = 2.0 / (PROJECTION_MATRIX[1][1] * VIEWPORT_SIZE.y);
		float meters_per_pixel = meters_away * pixel_size / facing;
		bump_meters = ground_detail(color, meters_per_pixel);
		float depth = water_level - ground_height(direction, 0.0);
		if (has_water && depth > 0.0) {
			color *= 1.0 + 0.9 * caustics(depth, meters_per_pixel);
		}
	}

	ALBEDO = color;
	EMISSION = weather_glow;
	ROUGHNESS = mix(0.9, 0.3, wet);  // Water catches the sun
	// A carved flat world's disc (ShapedGlobe) comes with its rim and underside, which are
	// bare rock, as the plain disc's are.
	bool flat_rock = flat_disc && lifted_on_cpu && eye_level_ground < 0.5
		&& (local_direction.y < -FLAT_HALF_THICKNESS + 1e-4
			|| length(local_direction.xz) > PI - 1e-4);
	if (lifted_on_cpu && !has_heights && !flat_rock) {
		// The carved globe's faces are flat: shade it as the round globe it is.
		vec3 round = flat_disc ? vec3(0.0, 1.0, 0.0) : direction;
		NORMAL = normalize((VIEW_MATRIX * MODEL_MATRIX * vec4(round, 0.0)).xyz);
	}

	if (has_heights && wet < 0.5 && !flat_rock) {
		// On a flat world, "up" is the face's, and slopes run across the disc.
		vec3 up = flat_disc ? vec3(0.0, 1.0, 0.0) : direction;
		vec3 normal = flat_disc
			? flat_relief_normal(local_direction.xz, pixel_angle)
			: relief_normal(direction, pixel_angle);
		if (map_shading) {
			ALBEDO *= map_shade(normal, up);
		} else {
			NORMAL = normalize((VIEW_MATRIX * MODEL_MATRIX * vec4(normal, 0.0)).xyz);
		}
	}

	if (eye_level_ground > 0.5) {
		// The bumps light and shade the ground's color by how they face the sun, rather than
		// tilting the normal: the painterly style lights in bands, which would turn small
		// tilts into stripes. Worked in the globe's own space (a rotation and an even scale
		// from the view's), where sun_direction is.
		mat3 to_view = mat3(VIEW_MATRIX) * mat3(MODEL_MATRIX);
		vec3 normal = normalize(transpose(to_view) * NORMAL);
		vec3 tilted = bumped(normal, bump_meters);
		float flat_light = max(dot(normal, sun_direction), 0.0) + 0.15;
		float bump_light = max(dot(tilted, sun_direction), 0.0) + 0.15;
		ALBEDO *= clamp(bump_light / flat_light, 0.6, 1.45);
	}

#ifdef STYLED
#ifdef PAINTERLY
	brush = brush_streaks(direction, pixel_angle);
#else
	brush = 0.5;
#endif
	// Outlined by the round globe's shape, not the mesh's: a carved globe's mesh has slivers
	// along its cuts whose normals jump about. A flat world isn't round, so it uses the mesh's.
	vec3 outline_normal = flat_disc
		? NORMAL
		: normalize((VIEW_MATRIX * MODEL_MATRIX * vec4(normalize(local_direction), 0.0)).xyz);
	ALBEDO = styled_albedo(ALBEDO, brush, outline_normal, VIEW);
#endif
	if (flat_rock) {
		ALBEDO = ROCK_COLOR;
		EMISSION = vec3(0.0);
		ROUGHNESS = 1.0;
	}
}
