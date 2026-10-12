// Lines and fills drawn on a globe's ground: roads, rivers, regions (see SurfaceOverlay.cs).
// Built on the ground itself and drawn over it, hills or not, as the points clicked to draw them
// are; only what's past the horizon (facing away from the camera) is hidden. Lifting them off
// the ground to show over it instead shifts them on screen when seen from close by.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, depth_test_disabled;

// A flat world's top face faces straight up (its own +Y); a globe's ground faces outward.
uniform bool flat_world = false;

// Above 0 where a point faces the camera, below 0 past the horizon.
varying float facing;

void vertex() {
    vec3 camera = (inverse(MODEL_MATRIX) * vec4(INV_VIEW_MATRIX[3].xyz, 1.0)).xyz;
    vec3 outward = flat_world ? vec3(0.0, 1.0, 0.0) : normalize(VERTEX);
    facing = dot(outward, normalize(camera - VERTEX));
}

void fragment() {
    if (facing < 0.0) {
        discard;
    }

    ALBEDO = COLOR.rgb;
    ALPHA = COLOR.a;
}
