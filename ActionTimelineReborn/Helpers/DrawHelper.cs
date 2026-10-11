using Dalamud.Interface.Textures.TextureWraps;
using ECommons.ImGuiMethods;

namespace ActionTimelineReborn.Helpers;

internal static class DrawHelper
{
    public static IDalamudTextureWrap? GetTextureFromIconId(uint iconId, bool highQuality = true)
        => ThreadLoadImageHandler.TryGetIconTextureWrap(iconId, highQuality, out IDalamudTextureWrap? texture) ? texture
        : ThreadLoadImageHandler.TryGetIconTextureWrap(0, highQuality, out texture) ? texture : null;
}
