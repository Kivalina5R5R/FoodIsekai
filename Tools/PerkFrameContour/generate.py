from pathlib import Path
from PIL import Image
import math
import bisect

# Bake the shared card image's opaque silhouette; no runtime texture readback is needed.
root = Path(__file__).resolve().parents[2]
source = root / 'Assets/Art/Perk/BG ui2.png'
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
body = '\n'.join(f'            new Vector2({x:.7f}f, {y:.7f}f),' for x, y in points)
code = '''using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Baked from the alpha contour of Assets/Art/Perk/BG ui2.png.
    // Regenerate with Tools/PerkFrameContour/generate.py when the frame artwork changes.
    internal static class PerkFrameContour
    {
        private static readonly Vector2[] points =
        {
''' + body + '''
        };

        // Samples by perimeter distance so the light follows the custom arch and shoulders.
        public static Vector2 Sample(Rect frame, float progress)
        {
            float position = Mathf.Repeat(progress, 1f) * points.Length;
            int index = Mathf.FloorToInt(position);
            Vector2 point = Vector2.Lerp(points[index], points[(index + 1) % points.Length], position - index);
            return frame.center + Vector2.Scale(point, frame.size);
        }
    }
}
'''
(root / 'Assets/Scripts/Display/PerkFrameContour.cs').write_text(code, encoding='utf-8')
print(f'Baked {len(points)} perimeter samples from {width}x{height} alpha silhouette.')
