// Live weather over the surface (VISION.md WTH-02): clouds, rain and snow, and the wind. It's
// drawn in the surface itself rather than on a shell above it: no second see-through layer to
// draw over the whole globe, and it's lit just like the ground in every style. Included by
// planet_surface.gdshaderinc, after the noise it uses.
//
// The weather is worked out in Core (LiveWeather) and arrives as two snapshots, which this
// blends between so it moves smoothly while the clock runs. Snapshot images run east from
// longitude −180° and south from the north pole. Weather: red is the cloud cover, green the
// rain, blue the snow (how hard it falls, on a log scale). Wind: red and green are the wind east
// and north, 0.5 for still air and 0 to 1 for −40 to 40 m/s.

uniform bool has_weather = false;
uniform sampler2D weather_old : filter_linear, repeat_enable;
uniform sampler2D weather_new : filter_linear, repeat_enable;
uniform sampler2D wind_old : filter_linear, repeat_enable;
uniform sampler2D wind_new : filter_linear, repeat_enable;

// How far from the old snapshot to the new one the clock is, 0 to 1.
uniform float weather_blend = 0.0;

uniform bool show_clouds = true;
uniform bool show_wind = false;

// Cloud detail (a quality setting): 0 draws the snapshot's own soft clouds; 1 adds fine noise
// to their edges.
uniform float cloud_detail = 1.0;

// The world clock in standard days, so the fine detail slowly changes.
uniform float weather_clock = 0.0;

const float WIND_RANGE_MS = 40.0;

// How much of the clouds still shows by night (the ambient light would otherwise make them
// glow there).
const float NIGHT_CLOUDS = 0.3;

float weather_hash(float n) {
	return fract(sin(n * 12.9898) * 43758.5453);
}

// A line of about a pixel along every whole number of `value` (which changes by `width` from
// pixel to pixel), fading out where lines would crowd closer than a few pixels.
float pixel_lines(float value, float width) {
	float from_line = abs(fract(value + 0.5) - 0.5);  // Distance to the nearest whole number
	float line = 1.0 - smoothstep(0.5 * width, 1.5 * width, from_line);
	return line * smoothstep(4.0, 8.0, 1.0 / max(width, 1e-6));
}

// Cloud edges in each style: crisp (Simple), soft and wandering (Painterly), soft (Realistic).
float cloud_edge(float cover, float fine) {
#ifdef SIMPLE
	float width = max(fwidth(cover), 0.01);
	return smoothstep(0.45 - width, 0.45 + width, cover + fine * 0.6);
#else
#ifdef PAINTERLY
	return smoothstep(0.3, 0.6, cover + fine);
#else
	return smoothstep(0.2, 0.75, cover + fine * 0.8);
#endif
#endif
}

// The wind as short streaks on a grid about 28 pixels apart (the spacing only changes in steps
// of two as the camera zooms): each points along the wind at its cell's middle, longer where it
// blows harder, brightest at its downwind end, with a pulse running along it. `ground` is
// radians east (shrunk toward the poles) and north.
float wind_streak(vec2 ground) {
	float pixel = length(fwidth(ground));
	float cell_size = exp2(ceil(log2(28.0 * pixel)));
	vec2 cell = floor(ground / cell_size);
	float seed = weather_hash(dot(cell, vec2(1.0, 57.0)));
	vec2 middle = (cell + 0.5 + (vec2(seed, weather_hash(seed * 31.0)) - 0.5) * 0.3) * cell_size;

	float middle_latitude = clamp(middle.y, -HALF_PI, HALF_PI);
	float middle_longitude = middle.x / max(cos(middle_latitude), 0.05);
	vec2 uv = vec2(middle_longitude / TAU + 0.5, 0.5 - middle_latitude / PI);
	vec2 wind = (mix(texture(wind_old, uv).rg, texture(wind_new, uv).rg, weather_blend) - 0.5)
		* 2.0 * WIND_RANGE_MS;
	float speed = length(wind);
	if (speed < 0.5) {
		return 0.0;
	}

	vec2 along_wind = wind / speed;
	vec2 offset = (ground - middle) / cell_size;  // In cells
	float along = dot(offset, along_wind);
	float across = dot(offset, vec2(-along_wind.y, along_wind.x));
	float half_length = 0.12 + 0.3 * clamp(speed / 20.0, 0.0, 1.0);
	float half_width = 0.7 * pixel / cell_size;  // About a pixel and a half wide
	float on_line = (1.0 - smoothstep(half_length - half_width, half_length, abs(along)))
		* (1.0 - smoothstep(half_width, 2.0 * half_width, abs(across)));
	float toward_head = clamp(along / (2.0 * half_length) + 0.5, 0.0, 1.0);
	float pulse = 1.0 - fract(toward_head - TIME * (0.4 + speed * 0.03) - seed);
	return on_line * (0.25 + 0.5 * toward_head + 0.25 * pulse * pulse);
}

// Rain's lines and snow's dots over the color (see draw_weather). `ground_width` is how much
// `ground` changes from pixel to pixel, worked out before any branching (where the GPU's
// measure of it can't be trusted).
vec3 draw_falling(vec3 color, vec2 ground, vec2 ground_width, float rain, float snow,
		float day) {
	float pixel = length(ground_width);
	float spacing = exp2(floor(log2(1.0 / max(7.0 * pixel, 1e-6))));
	float rain_marks = pixel_lines((ground.x + ground.y * 2.5) * spacing,
		(ground_width.x + 2.5 * ground_width.y) * spacing) * smoothstep(0.05, 0.3, rain);
	vec2 cell = ground * spacing * 0.7;
	vec2 offset = fract(cell) - 0.5;
	float cell_pixel = length(ground_width) * spacing * 0.7;
	float dot_size = 1.6 * cell_pixel;
	float snow_marks = (1.0 - smoothstep(dot_size * 0.6, dot_size, length(offset)))
		* smoothstep(0.05, 0.3, snow) * smoothstep(4.0, 8.0, 1.0 / cell_pixel);
	color = mix(color, vec3(0.45, 0.62, 0.95), rain_marks * 0.8 * day);
	return mix(color, vec3(1.0), snow_marks * day);
}

// The surface color with the weather over it, at a direction and its latitude and longitude
// (radians). `glow` gets the wind's own light, so it shows by night too.
vec3 draw_weather(vec3 color, vec3 direction, float latitude, float longitude, out vec3 glow) {
	glow = vec3(0.0);
	vec2 uv = vec2(longitude / TAU + 0.5, 0.5 - latitude / PI);
	vec4 weather = mix(texture(weather_old, uv), texture(weather_new, uv), weather_blend);
	vec2 ground = vec2(longitude * cos(latitude), latitude);
	vec2 ground_width = fwidth(ground);

	if (show_clouds) {
		float fine = 0.0;
		if (cloud_detail > 0.0) {
			vec3 p = direction * 9.0 + vec3(0.0, 0.0, weather_clock * 0.15);
			fine = (value_noise(p) * 0.65 + value_noise(p * 2.3 + 5.0) * 0.35 - 0.5)
				* 0.45 * cloud_detail;
#ifdef PAINTERLY
			// Stretched along the latitudes, like brush strokes.
			fine += (value_noise(direction * vec3(10.0, 34.0, 10.0)) - 0.5) * 0.2 * cloud_detail;
#endif
		}
		float rain = weather.g;
		float snow = weather.b;

		// Thin cloud is see-through, thick cores nearly hide the ground, and by night the
		// clouds mostly fade (a flat world's face is all lit alike).
		float day = flat_disc ? 1.0 : smoothstep(-0.15, 0.1, dot(direction, sun_direction));
		float cover = cloud_edge(weather.r, fine)
			* mix(0.6, 0.92, smoothstep(0.45, 0.9, weather.r))
			* mix(NIGHT_CLOUDS, 1.0, day);

		// Rain clouds are darker; snow clouds stay bright.
		vec3 cloud_color = mix(vec3(0.95), vec3(0.45, 0.5, 0.58), clamp(rain * 2.5, 0.0, 0.75));
		color = mix(color, cloud_color, cover);

		// Rain as fine slanting lines about 7 pixels apart, snow as dots about 10 apart, where
		// it falls hard enough to see (spacings that change in steps of two as the camera
		// zooms). Most of the globe is dry, and skips this.
		if (max(rain, snow) > 0.05) {
			color = draw_falling(color, ground, ground_width, rain, snow, day);
		}
	}

	if (show_wind) {
		float streak = wind_streak(ground);
		color = mix(color, vec3(0.8, 0.97, 1.0), streak);
		glow = vec3(0.25, 0.32, 0.35) * streak;
	}

	return color;
}
