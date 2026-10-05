// Rain and snow falling around a first-person eye (VISION.md REN-06; owner's choice: light
// screen streaks and flakes, cheap enough for the Base tier). Drawn over the whole view as a few
// layers of streaks (rain) or drifting dots (snow), nearer layers bigger and faster, slanted by
// the wind blowing across the view. How hard it falls comes from the live weather at the spot.
shader_type canvas_item;

uniform float rain = 0.0;   // 0 (none) to 1 (a downpour)
uniform float snow = 0.0;   // 0 (none) to 1 (a blizzard)
uniform float slant = 0.0;  // Sideways drift for each unit of fall (wind across the view)
uniform float light = 1.0;  // How bright it is: 1 by day, low by night

float hash(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

// One layer of rain streaks: tall cells, some holding a streak, falling at `speed`.
float rain_layer(vec2 uv, float scale, float speed, float seed) {
	vec2 p = uv * vec2(scale, scale * 0.2);
	p.x += p.y * slant * 5.0;
	p.y += TIME * speed;
	vec2 cell = floor(p);
	vec2 inside = fract(p);
	float chance = hash(cell + seed);
	if (chance > rain) {
		return 0.0;
	}

	float across = abs(inside.x - (0.2 + 0.6 * hash(cell + seed + 7.1)));
	float along = fract(inside.y + hash(cell + seed + 3.3));
	float line = 1.0 - smoothstep(0.0, 0.05, across);
	float span = smoothstep(0.0, 0.1, along) * (1.0 - smoothstep(0.45, 0.55, along));
	return line * span;
}

// One layer of snowflakes: dots drifting down and wobbling side to side.
float snow_layer(vec2 uv, float scale, float speed, float seed) {
	vec2 p = uv * scale;
	p.y += TIME * speed;
	p.x += p.y * slant + sin(TIME * 1.3 + p.y * 2.0 + seed) * 0.15;
	vec2 cell = floor(p);
	vec2 inside = fract(p);
	if (hash(cell + seed) > snow) {
		return 0.0;
	}

	vec2 middle = vec2(hash(cell + seed + 1.7), hash(cell + seed + 9.2)) * 0.6 + 0.2;
	return 1.0 - smoothstep(0.04, 0.09, length(inside - middle));
}

void fragment() {
	float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
	vec2 uv = vec2(SCREEN_UV.x * aspect, -SCREEN_UV.y);

	float streaks = 0.0;
	float flakes = 0.0;
	if (rain > 0.0) {
		streaks = max(rain_layer(uv, 70.0, 7.0, 0.0) * 0.6,
			max(rain_layer(uv, 40.0, 5.0, 17.0) * 0.8, rain_layer(uv, 22.0, 3.6, 41.0)));
	}
	if (snow > 0.0) {
		flakes = max(snow_layer(uv, 40.0, 1.2, 0.0) * 0.6,
			max(snow_layer(uv, 24.0, 0.9, 13.0) * 0.8, snow_layer(uv, 14.0, 0.6, 29.0)));
	}

	// A grey veil in heavy rain or snow, then the streaks and flakes over it.
	float veil = 0.25 * max(rain, snow * 0.8);
	vec3 color = vec3(0.55, 0.6, 0.66) * light;
	float alpha = veil + streaks * 0.35 + flakes * 0.8;
	color = mix(color, vec3(0.75, 0.8, 0.88) * light, streaks / max(alpha, 1e-4) * 0.35);
	color = mix(color, vec3(1.0) * max(light, 0.15), flakes * 0.8 / max(alpha, 1e-4));
	COLOR = vec4(color, clamp(alpha, 0.0, 0.85));
}
