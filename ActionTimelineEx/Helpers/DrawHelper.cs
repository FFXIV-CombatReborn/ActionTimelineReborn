using Dalamud.Interface.Internal;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Lumina.Data.Files;
using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;

namespace ActionTimeline.Helpers;

internal static class DrawHelper
{
    private static readonly Vector2 _uv1 = new (96 * 5 / 852f, 0),
    _uv2 = new ((96 * 5 + 144) / 852f, 0.5f);

    private static IDalamudTextureWrap? _roundTex;
    public static void Init()
    {
        ThreadLoadImageHandler.TryGetTextureWrap("ui/uld/icona_frame_hr1.tex", out _roundTex);
    }

    public static void DrawDamage(this ImDrawListPtr drawList, Vector2 position, float size, uint  lightCol)
    {
        if(_roundTex == null) return;

        var pixPerUnit = size / 82;

        var outPos = position - new Vector2(pixPerUnit * 31, pixPerUnit * 31);
        drawList.AddImage(_roundTex.ImGuiHandle, outPos, outPos + new Vector2(pixPerUnit * 144, pixPerUnit * 154),
        _uv1, _uv2, lightCol);
    }

    public static void DrawActionIcon(this ImDrawListPtr drawList, uint iconId, bool isHq, Vector2 position, float size)
    {
        IDalamudTextureWrap? texture = GetTextureFromIconId(iconId, isHq);
        if (texture == null) return;

        var pixPerUnit = size / 82;

        drawList.AddImage(texture.ImGuiHandle, position, position + new Vector2(size));

        //Cover.
        if (ThreadLoadImageHandler.TryGetTextureWrap("ui/uld/icona_frame_hr1.tex", out var frameText))
        {
            var coverPos = position - new Vector2(pixPerUnit * 3, pixPerUnit * 4);
            drawList.AddImage(frameText.ImGuiHandle, coverPos, coverPos + new Vector2(pixPerUnit * 88, pixPerUnit * 96),
                new Vector2(4f / frameText.Width, 0f / frameText.Height), new Vector2(92f / frameText.Width, 96f / frameText.Height));
        }
    }

    public static Vector4 ChangeAlpha(this Vector4 color, float alpha)
    {
        color.Z = alpha;
        return color;
    }

    public static IDalamudTextureWrap? GetTextureFromIconId(uint iconId, bool highQuality = true)
        => ThreadLoadImageHandler.TryGetIconTextureWrap(iconId, highQuality, out var texture) ? texture 
        : ThreadLoadImageHandler.TryGetIconTextureWrap(0, highQuality, out texture) ? texture : null;

    private static readonly Dictionary<uint, uint> textureColorCache = [];
    private static readonly Queue<uint> calculating = new ();
    public static uint GetTextureAverageColor(uint iconId)
    {
        if (textureColorCache.TryGetValue(iconId, out var color)) return color;

        if (!calculating.Contains(iconId)) calculating.Enqueue(iconId);

        CalculateColor();
        return uint.MaxValue;
    }

    private static bool _run;
    private static void CalculateColor()
    {
        if (_run) return;
        _run = true;

        Task.Run(() =>
        {
            while(calculating.TryDequeue(out var icon))
            {
                var tex = Svc.Data.GetFile<TexFile>($"ui/icon/{icon/1000:D3}000/{icon:D6}.tex");
                if(tex == null)
                {
                    textureColorCache[icon] = uint.MaxValue;
                    continue;
                }

                byte[] imageData = tex.ImageData;
                float whole = 0, r = 0, g = 0, b = 0;
                for (int i = 0; i < imageData.Length; i += 4)
                {
                    var alpha = imageData[i + 3] / (float)byte.MaxValue;
                    b += imageData[i] / (float)byte.MaxValue * alpha;
                    g += imageData[i + 1] / (float)byte.MaxValue * alpha;
                    r += imageData[i + 2] / (float)byte.MaxValue * alpha;

                    whole += alpha;
                }

                textureColorCache[icon] = ImGui.ColorConvertFloat4ToU32(new Vector4(r / whole, g / whole, b / whole, 1));
            }
            _run = false;
        });
    }

    public static void SetTooltip(string message)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(message);
        }
    }

    public static bool IsInRect(Vector2 leftTop, Vector2 size)
    {
        var pos = ImGui.GetMousePos() - leftTop;
        if (pos.X < 0 || pos.Y < 0 || pos.X > size.X || pos.Y > size.Y) return false;
        return true;
    }
}
