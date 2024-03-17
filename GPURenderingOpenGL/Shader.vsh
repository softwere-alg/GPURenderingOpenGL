// vertex position
attribute vec2 position;
// texture coordinate
attribute vec2 texCoordinate;

// texture coordinates to pass to fragment shader
varying lowp vec2 outTexCoordinate;

// viewport size
uniform ivec2 viewportSize;
// model matrix
uniform mat3 modelMatrix;

void main()
{
    // multiply model matrix to apply translation, rotation, and scale
    vec2 pixelSpacePosition = (modelMatrix * vec3(position, 1.0)).xy;
    // cast to float type
    vec2 fViewportSize = vec2(viewportSize);

    // To convert from positions in pixel space to positions in clip-space,
    //  divide the pixel coordinates by half the size of the viewport.
    gl_Position = vec4(0.0, 0.0, 0.0, 1.0);
    gl_Position.xy = pixelSpacePosition / (fViewportSize / 2.0);

    // pass to fragment shader
    outTexCoordinate = texCoordinate;
}
