// vertex position
attribute vec3 position;
// texture coordinate
attribute vec2 texCoordinate;

// texture coordinates to pass to fragment shader
varying lowp vec2 outTexCoordinate;

// viewport size
uniform ivec2 viewportSize;
// model matrix
uniform mat4 modelMatrix;
// view matrix
uniform mat4 viewMatrix;

void main()
{
    // multiply model matrix to apply translation, rotation, and scale
    // multiply view matrix to change camera position
    vec2 pixelSpacePosition = (viewMatrix * modelMatrix * vec4(position, 1.0)).xy;
    // cast to float type
    vec2 fViewportSize = vec2(viewportSize);

    // To convert from positions in pixel space to positions in clip-space,
    //  divide the pixel coordinates by half the size of the viewport.
    gl_Position = vec4(0.0, 0.0, 0.0, 1.0);
    gl_Position.xy = pixelSpacePosition / (fViewportSize / 2.0);

    // pass to fragment shader
    outTexCoordinate = texCoordinate;
}
