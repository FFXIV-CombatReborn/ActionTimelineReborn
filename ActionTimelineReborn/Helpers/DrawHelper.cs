using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using Lumina.Data.Files;
using System.Numerics;

namespace ActionTimelineReborn.Helpers;

internal static class DrawHelper
{
    public static IDalamudTextureWrap? GetTextureFromIconId(uint iconId, bool highQuality = true)
        => ThreadLoadImageHandler.TryGetIconTextureWrap(iconId, highQuality, out IDalamudTextureWrap? texture) ? texture
        : ThreadLoadImageHandler.TryGetIconTextureWrap(0, highQuality, out texture) ? texture : null;

    private static readonly Dictionary<uint, uint> textureColorCache = [];
    private static readonly Queue<uint> calculating = new();
    private static readonly HashSet<uint> calculatingSet = []; // Track items being calculated for O(1) lookup

    public static uint GetTextureAverageColor(uint iconId)
    {
        if (textureColorCache.TryGetValue(iconId, out uint color))
        {
            return color;
        }

        // Use HashSet for O(1) lookup instead of Queue.Contains which is O(n)
        if (!calculatingSet.Contains(iconId))
        {
            calculating.Enqueue(iconId);
            calculatingSet.Add(iconId);
        }

        CalculateColor();
        return uint.MaxValue;
    }

    private static bool _run;
    private static void CalculateColor()
    {
        if (_run)
        {
            return;
        }

        _run = true;

        Task.Run(() =>
        {
            while (calculating.TryDequeue(out uint icon))
            {
                // Remove from calculating set when processing
                calculatingSet.Remove(icon);

                TexFile? tex = Svc.Data.GetFile<TexFile>($"ui/icon/{icon / 1000:D3}000/{icon:D6}.tex");
                if (tex == null)
                {
                    textureColorCache[icon] = uint.MaxValue;
                    continue;
                }

                byte[] imageData = tex.ImageData;
                float whole = 0, r = 0, g = 0, b = 0;
                for (int i = 0; i < imageData.Length; i += 4)
                {
                    float alpha = imageData[i + 3] / (float)byte.MaxValue;
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
}
