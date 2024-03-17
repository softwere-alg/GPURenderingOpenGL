// texture coordinate from vertex shader
varying lowp vec2 outTexCoordinate;

// texture
uniform sampler2D s_texture;

void main()
{
    // sample texture by texture coordinates
    gl_FragColor = texture2D(s_texture, outTexCoordinate);
}
