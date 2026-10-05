// The live weather's cloud layer as seen from close by in first person (VISION.md REN-06):
// how thick the cloud is at a point on the layer, and its color before light. Shared by the
// sky (surface_sky.gdshader, the clouds overhead from below) and the cloud deck
// (cloud_deck.gdshader, the clouds underneath from above), so both draw the same clouds.
//
// The cover comes from the same weather snapshots the globe draws (planet_weather.gdshaderinc
// has their layout), with fine detail some km across from a tiling noise image laid flat
// around the spot, far cheaper than working noise out per pixel.

uniform sampler2D weather_old : filter_linear, repeat_enable;
uniform sampler2D weather_new : filter_linear, repeat_enable;
uniform float weather_blend = 0.0;
uniform float weather_clock = 0.0;  // Standard days, so the fine detail slowly changes
uniform float cloud_detail = 1.0;   // A quality setting: 0 leaves the snapshots' soft clouds

// The detail's noise, laid along the ground around the spot (east and north, in the body's
// frame), and its scale (the body's radius over the detail's size, in km).
uniform sampler2D cloud_noise : filter_linear_mipmap, repeat_enable;
uniform vec3 ground_east = vec3(1.0, 0.0, 0.0);
uniform vec3 ground_north = vec3(0.0, 0.0, 1.0);
uniform float cloud_noise_scale = 2000.0;

// The cloud at a point on the layer (a unit direction in the body's frame) seen from
// distance_km away: x is its cover (0 clear to 1 thick), yzw its color before light, grey
// where rain falls and darker where it's thick. The fine detail fades with distance, where
// it would only shimmer.
vec4 cloud_at(vec3 at, float distance_km) {
	float latitude = asin(clamp(at.y, -1.0, 1.0));
	float longitude = atan(at.x, at.z);
	vec2 uv = vec2(longitude / TAU + 0.5, 0.5 - latitude / PI);
	vec4 weather = mix(texture(weather_old, uv), texture(weather_new, uv), weather_blend);

	float fine = 0.0;
	if (cloud_detail > 0.0) {
		vec2 along_ground = vec2(dot(at, ground_east), dot(at, ground_north)) * cloud_noise_scale
			+ vec2(weather_clock * 30.0, 0.0);
		fine = (texture(cloud_noise, along_ground / 8.0).r * 0.6
			+ texture(cloud_noise, along_ground / 8.0 * 2.7 + 0.37).r * 0.4 - 0.5)
			* 0.9 * cloud_detail * (1.0 - smoothstep(20.0, 120.0, distance_km));
	}
	float cover = smoothstep(0.25, 0.7, weather.r + fine)
		* mix(0.7, 1.0, smoothstep(0.5, 0.9, weather.r));  // Thick cloud hides the stars

	vec3 color = mix(vec3(1.0), vec3(0.5, 0.53, 0.6), clamp(weather.g * 2.5, 0.0, 0.75));
	color *= mix(1.0, 0.65, smoothstep(0.6, 1.0, weather.r));  // Thick cloud's darker body
	return vec4(cover, color);
}
