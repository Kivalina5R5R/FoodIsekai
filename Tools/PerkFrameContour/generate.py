from pathlib import Path
from PIL import Image
import math
import bisect

# Bake the card images' opaque silhouettes; no runtime texture readback is needed.
root = Path(__file__).resolve().parents[2]
SMALL = root / 'Assets/Art/Perk/BG ui2.png'
BIG = root / 'Assets/Art/Perk/BG_Card_Big.png'


def bake(source):
    alpha = Image.open(source).getchannel('A')
    width, height = alpha.size
    cx, cy = width / 2, height / 2

    def opaque(x, y):
        return 0 <= x < width and 0 <= y < height and alpha.getpixel((int(x), int(y))) >= 128

    outline = []
    for i in range(4096):
        angle = -math.pi / 2 + math.tau * i / 4096
        dx, dy = math.cos(angle), math.sin(angle)
        low, high = 0.0, math.hypot(width, height)
        for _ in range(22):
            radius = (low + high) / 2
            if opaque(cx + dx * radius, cy + dy * radius):
                low = radius
            else:
                high = radius
        outline.append((cx + dx * low, cy + dy * low))
    outline.append(outline[0])
    distances = [0.0]
    for a, b in zip(outline, outline[1:]):
        distances.append(distances[-1] + math.dist(a, b))
    points = []
    for i in range(256):
        distance = distances[-1] * i / 256
        j = min(bisect.bisect_right(distances, distance) - 1, len(outline) - 2)
        t = (distance - distances[j]) / (distances[j + 1] - distances[j])
        x = outline[j][0] + (outline[j + 1][0] - outline[j][0]) * t
        y = outline[j][1] + (outline[j + 1][1] - outline[j][1]) * t
        points.append((x / width - .5, .5 - y / height))
    return points, width, height


def body(points):
    return '\n'.join(f'            new Vector2({x:.7f}f, {y:.7f}f),' for x, y in points)


small, small_width, small_height = bake(SMALL)
big, big_width, big_height = bake(BIG)
# Shop layout rects are authored for the Small frame; the Big frame is drawn at its own native aspect.
big_height_scale = (big_height / big_width) / (small_height / small_width)
code = '''using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Baked from the alpha contours of Assets/Art/Perk/BG ui2.png (Small) and BG_Card_Big.png (Big).
    // Regenerate with Tools/PerkFrameContour/generate.py when the frame artwork changes.
    internal static class PerkFrameContour
    {
        // Height of the Big frame relative to a rect authored at the Small frame's aspect.
        public const float BigHeightScale = ''' + f'{big_height_scale:.7f}' + '''f;

        private static readonly Vector2[] points =
        {
''' + body(small) + '''
        };

        private static readonly Vector2[] bigPoints =
        {
''' + body(big) + '''
        };

        // Samples by perimeter distance so the light follows the custom arch and shoulders.
        public static Vector2 Sample(Rect frame, float progress, bool big = false)
        {
            Vector2[] contour = big ? bigPoints : points;
            float position = Mathf.Repeat(progress, 1f) * contour.Length;
            int index = Mathf.FloorToInt(position);
            Vector2 point = Vector2.Lerp(contour[index], contour[(index + 1) % contour.Length], position - index);
            return frame.center + Vector2.Scale(point, frame.size);
        }
    }
}
'''
(root / 'Assets/Scripts/Display/PerkFrameContour.cs').write_text(code, encoding='utf-8')
print(f'Baked {len(small)} Small samples from {small_width}x{small_height} and {len(big)} Big samples '
      f'from {big_width}x{big_height}; Big height scale {big_height_scale:.4f}.')
