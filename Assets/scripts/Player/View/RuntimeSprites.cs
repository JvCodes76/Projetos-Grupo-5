using UnityEngine;

/// <summary>Sprites gerados em runtime para os placeholders visuais (visor, pips, retícula). Criados uma vez.</summary>
public static class RuntimeSprites
{
    private static Sprite whitePixel;

    /// <summary>Pixel branco de 1 × 1 com 32 PPU (1/32 u; escale o transform para o tamanho desejado).</summary>
    public static Sprite WhitePixel
    {
        get
        {
            if (whitePixel != null) return whitePixel;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                name = "RuntimeWhitePixel",
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            whitePixel = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 32f);
            whitePixel.name = "RuntimeWhitePixel";
            whitePixel.hideFlags = HideFlags.DontSave;
            return whitePixel;
        }
    }
}
