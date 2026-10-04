// The Painterly and Simple styles' own look (VISION.md REN-05; owner's choice: made in the
// surface shader rather than a filter over the screen, so it's cheap, and labels, grid lines,
// and handles stay sharp). Included by planet_surface.gdshaderinc, after the noise it uses.
//
// Painterly: soft bands of light whose edges wander like brush strokes, a warm glow where day
// turns to night, streaks of brush strokes over the surface, and a soft outline.
// Simple: even light with a crisp line between day and night, and a clean dark outline.
//
// The light() below replaces Godot's own lighting, which Realistic keeps (planet.gdshader).
// The star's light doesn't fade with distance (SystemView), so ATTENUATION is only shadow.

#ifdef PAINTERLY
const float LIGHT_BANDS = 3.0;
const float BAND_WANDER = 0.6;        // How far the brush moves a band's edge, in bands
const float OUTLINE_PIXELS = 4.0;
const float OUTLINE_SOFTNESS = 3.0;   // Pixels over which the outline fades in
const float OUTLINE_STRENGTH = 0.75;
#else
const float OUTLINE_PIXELS = 2.5;
const float OUTLINE_SOFTNESS = 0.75;
const float OUTLINE_STRENGTH = 0.9;
const vec3 OUTLINE_INK = vec3(0.05, 0.06, 0.08);
#endif

// The brush streak under each pixel (0 to 1), passed on to light() to wander the bands.
varying float brush;

// Brush streaks: noise stretched along the latitudes, like strokes laid on side to side. They
// fade to an even 0.5 where they'd be finer than a few pixels, so distant worlds don't shimmer.
float brush_streaks(vec3 direction, float pixel_angle) {
	vec3 p = direction * vec3(14.0, 48.0, 14.0);
	float streaks = value_noise(p) * 0.65 + value_noise(p * 2.1 + 7.0) * 0.35;
	// A streak is about 1/48 of a radius tall: fully shown above about 3 pixels.
	float shown = 1.0 - smoothstep(0.007, 0.02, pixel_angle);
	return mix(0.5, streaks, shown);
}

// How much outline a pixel gets: 1 at the edge of the globe, fading to 0 about OUTLINE_PIXELS
// in, however big the globe is on screen. Near the edge, facing (the cosine between the surface
// and the view) over its change per pixel is about twice the pixels from the edge.
float outline(vec3 normal, vec3 view) {
	float facing = max(dot(normal, view), 0.0);
	float pixels_in = facing / max(fwidth(facing), 1e-5) * 0.5;
	return 1.0 - smoothstep(OUTLINE_PIXELS - OUTLINE_SOFTNESS, OUTLINE_PIXELS + OUTLINE_SOFTNESS,
		pixels_in);
}

// The surface color in this style: brushed (Painterly) and outlined. `streak` is the brush.
vec3 styled_albedo(vec3 albedo, float streak, vec3 normal, vec3 view) {
#ifdef PAINTERLY
	albedo *= 0.9 + 0.2 * streak;
	vec3 ink = albedo * 0.35;  // A darker shade of the surface, not black
#else
	vec3 ink = OUTLINE_INK;
#endif
	return mix(albedo, ink, outline(normal, view) * OUTLINE_STRENGTH);
}

// The bands and the day/night line come from the angle to the light alone, and shadow (an
// eclipse) is applied after. Mixing shadow into the bands would show the speckled self-shadow
// a globe gets where the light grazes it, which true lighting hides by being dim there.
void light() {
	float facing = max(dot(NORMAL, LIGHT), 0.0);
#ifdef PAINTERLY
	// Bands with soft steps between them; the brush wobbles where each step falls.
	float x = facing * LIGHT_BANDS + (brush - 0.5) * BAND_WANDER;
	float level = clamp((floor(x) + smoothstep(0.3, 0.7, fract(x))) / LIGHT_BANDS, 0.0, 1.0);
	float dusk = smoothstep(0.05, 0.15, facing) * (1.0 - smoothstep(0.15, 0.4, facing));
	DIFFUSE_LIGHT += LIGHT_COLOR / PI * ATTENUATION * (level + dusk * vec3(0.3, 0.12, 0.0));
#else
	// Day is evenly lit, with a crisp line where it turns to night.
	float level = smoothstep(0.03, 0.06, facing);
	DIFFUSE_LIGHT += LIGHT_COLOR / PI * ATTENUATION * level * 0.85;
#endif
}
