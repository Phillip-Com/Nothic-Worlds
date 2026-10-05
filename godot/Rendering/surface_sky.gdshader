// The sky seen from a world's surface in first person (VISION.md REN-06): blue by day, glowing
// at sunrise and sunset, dark at night with stars and nebulas; black always on a world without
// air. The sun and the other bodies are drawn as discs at their true size and place (or
// enlarged, with Magnify), lit on the side facing their star, so moons show their phases. The
// places come from Core (SkyView); directions are in the scene's frame, the system's. The live
// weather's clouds pass overhead: the same snapshots the globe draws (planet_weather.gdshaderinc)
// read where each line of sight meets a layer of cloud a few km up, so they match the globe's.
shader_type sky;

const int MAX_BODIES = 12;

uniform sampler2D nebula_sky : filter_linear, repeat_enable;
uniform bool has_nebulas = false;
uniform vec3 space_color = vec3(0.02, 0.02, 0.04);  // Screen color, as the background is set

uniform vec3 local_up = vec3(0.0, 1.0, 0.0);
uniform bool has_air = true;

uniform bool has_star = false;
uniform vec3 star_direction = vec3(0.0, 1.0, 0.0);
uniform float star_radius = 0.0047;  // Radians
uniform vec3 star_color = vec3(1.0, 0.95, 0.8);

// Other bodies: direction, angular radius (radians), color, the way from each to its star (to
// light the right side), and whether it shines itself (a star).
uniform int body_count = 0;
uniform vec3 body_directions[MAX_BODIES];
uniform float body_radii[MAX_BODIES];
uniform vec3 body_colors[MAX_BODIES];
uniform vec3 body_light[MAX_BODIES];
uniform float body_shines[MAX_BODIES];

// The live weather's clouds overhead (the cover itself is in cloud_layer.gdshaderinc).
#include "res://Rendering/cloud_layer.gdshaderinc"
uniform bool has_clouds = false;

// The layer of cloud, in the body's own frame and radii: the turn from the scene's frame to
// the body's, the eye's direction and distance from the body's middle, and the cloud layer's
// radius squared less the eye's (set apart, as the difference is too fine for a float to keep
// from the two).
uniform mat3 to_body = mat3(1.0);
uniform vec3 eye_direction = vec3(0.0, 1.0, 0.0);
uniform float eye_radius = 1.0;
uniform float cloud_gap = 0.0016;
uniform float km_per_radius = 6371.0;

vec3 to_linear(vec3 color) {
	vec3 low = color / 12.92;
	vec3 high = pow((color + 0.055) / 1.055, vec3(2.4));
	return mix(high, low, vec3(lessThanEqual(color, vec3(0.04045))));
}

float hash(vec3 p) {
	p = fract(p * 0.1031);
	p += dot(p, p.zyx + 31.32);
	return fract((p.x + p.y) * p.z);
}

// Space behind everything: the nebula backdrop (or the plain background) and stars, fixed to the
// system, so they wheel overhead as the world turns.
vec3 space(vec3 direction) {
	vec3 color;
	if (has_nebulas) {
		float latitude = asin(clamp(direction.y, -1.0, 1.0));
		float longitude = atan(direction.x, direction.z);
		color = to_linear(texture(nebula_sky, vec2(longitude / TAU + 0.5, 0.5 - latitude / PI)).rgb);
	} else {
		color = to_linear(space_color);
	}

	// One star in some cells of a fine grid on the sky, a little off its cell's middle.
	vec3 cell = floor(direction * 260.0);
	float chance = hash(cell);
	if (chance > 0.985) {
		vec3 spot = (cell + 0.25 + 0.5 * vec3(hash(cell + 3.1), hash(cell + 7.7), hash(cell + 1.9)))
			/ 260.0;
		float distance = length(normalize(spot) - direction);
		float brightness = (chance - 0.985) / 0.015;
		color += vec3(0.9, 0.93, 1.0) * brightness * brightness * 3.0
			* (1.0 - smoothstep(0.0006, 0.0016, distance));
	}
	return color;
}

// The sky's own glow by day: deep blue overhead, paler at the horizon, warm toward a low sun.
vec3 daylight(vec3 direction, float sun_height) {
	float up = max(dot(direction, local_up), 0.0);
	vec3 sky = mix(vec3(0.55, 0.7, 0.92), vec3(0.12, 0.3, 0.78), pow(up, 0.5));
	float toward_sun = max(dot(direction, star_direction), 0.0);
	float low_sun = 1.0 - smoothstep(0.0, 0.3, abs(sun_height));
	float near_horizon = 1.0 - smoothstep(0.0, 0.5, up);
	sky = mix(sky, vec3(1.0, 0.45, 0.16), low_sun * pow(toward_sun, 3.0) * near_horizon);
	return sky;
}

// A body's disc at a direction, or nothing (alpha 0): lit on the side toward its star.
vec4 disc(vec3 direction, vec3 middle, float radius, vec3 color, vec3 light, float shines) {
	float across = length(direction - middle * dot(direction, middle)) / sin(radius);
	if (dot(direction, middle) <= 0.0 || across > 1.0) {
		return vec4(0.0);
	}

	// The point on the ball seen there: its face's normal turns toward us at the middle.
	vec3 sideways = (direction - middle * dot(direction, middle)) / sin(radius);
	vec3 normal = sideways - middle * sqrt(max(1.0 - across * across, 0.0));
	// Sunlit rock far outshines the night sky: brighter than its plain color.
	float lit = shines > 0.5 ? 1.0 : max(dot(normal, light), 0.0) * 2.5 + 0.015;
	float edge = 1.0 - smoothstep(0.92, 1.0, across);  // A soft rim, against shimmering
	return vec4(color * lit, edge);
}

// The cloud seen along a direction (alpha 0 for none): lit by day, warm toward a low sun,
// grey where rain falls, dark by night. Seen from below the layer only; from above, the cloud
// deck (cloud_deck.gdshader) draws them over the ground.
vec4 clouds(vec3 direction, float day, float sun_height) {
	if (!has_clouds || cloud_gap <= 0.0) {
		return vec4(0.0);
	}

	// Where the line of sight meets the layer: |eye + t·d| = layer's radius.
	vec3 d = to_body * direction;
	float b = eye_radius * dot(eye_direction, d);
	float t = -b + sqrt(b * b + cloud_gap);
	vec3 at = normalize(eye_direction * eye_radius + d * t);
	vec4 cloud = cloud_at(at, t * km_per_radius);
	float cover = cloud.x;
	vec3 color = cloud.yzw;
	float toward_sun = max(dot(direction, star_direction), 0.0);
	float low_sun = 1.0 - smoothstep(0.0, 0.25, abs(sun_height));
	color = mix(color, vec3(1.0, 0.55, 0.3), low_sun * (0.3 + 0.5 * toward_sun));
	color *= 0.006 + 0.95 * day;
	return vec4(color, cover);
}

// Everything but the clouds: space and its stars, the daylight, the bodies, and the sun.
vec3 sharp_sky(vec3 direction, float sun_height, float day) {
	// Space (and its stars) show only once the sun is well down: gone by the end of twilight.
	float night = has_air ? 1.0 - smoothstep(-0.2, -0.04, sun_height) : 1.0;
	vec3 color = space(direction) * night + daylight(direction, sun_height) * day;

	for (int i = 0; i < body_count; i++) {
		vec4 seen = disc(direction, body_directions[i], body_radii[i], body_colors[i],
			body_light[i], body_shines[i]);
		// Seen through the day sky, a moon is pale: its light adds to the sky's.
		color = mix(color, color * day + seen.rgb, seen.a);
	}

	if (has_star) {
		float angle = acos(clamp(dot(direction, star_direction), -1.0, 1.0));
		float edge = 1.0 - smoothstep(star_radius * 0.92, star_radius, angle);
		color = mix(color, star_color * 12.0, edge);
		if (has_air) {
			// The glare around the sun in air, gone once it has set.
			color += star_color * 0.6 * exp(-angle / 0.06) * max(day, 0.3)
				* smoothstep(-0.03, 0.02, sun_height);
		}
	}

	return color;
}

void sky() {
	vec3 direction = normalize(EYEDIR);
	float sun_height = has_star ? dot(star_direction, local_up) : -1.0;

	// How much daylight the air scatters: full a little after sunrise, a quarter as the sun
	// touches the horizon, and nearly gone once it's 3° below.
	float dawn = smoothstep(-0.1, 0.1, sun_height);
	float day = has_air ? dawn * dawn : 0.0;

	vec4 cloud = clouds(direction, day, sun_height);
	COLOR = mix(sharp_sky(direction, sun_height, day), cloud.rgb, cloud.a);
}
