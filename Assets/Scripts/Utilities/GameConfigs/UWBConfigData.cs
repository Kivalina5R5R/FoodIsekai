using UnityEngine;

namespace FoodIsekaiZ.Configuration
{

    [System.Serializable]
    public class UWBAxisConversion
    {
        [Tooltip("Where the tracker's raw X goes in Unity. Tokens: x, -x, y, -y, z, -z, none.")]
        public string rawXTo = "z";
        [Tooltip("Where the tracker's raw Y goes in Unity. Tokens: x, -x, y, -y, z, -z, none.")]
        public string rawYTo = "x";
        [Tooltip("Where the tracker's raw Z goes in Unity. Tokens: x, -x, y, -y, z, -z, none.")]
        public string rawZTo = "none";

        public Vector3 Apply(Vector3 raw)
        {
            Vector3 result = Vector3.zero;
            Accumulate(rawXTo, raw.x, ref result);
            Accumulate(rawYTo, raw.y, ref result);
            Accumulate(rawZTo, raw.z, ref result);
            return result;
        }

        public void Validate()
        {
            int[] destinationCounts = new int[3];
            CountDestination(rawXTo, nameof(rawXTo), destinationCounts);
            CountDestination(rawYTo, nameof(rawYTo), destinationCounts);
            CountDestination(rawZTo, nameof(rawZTo), destinationCounts);

            string[] axisNames = { "X", "Y", "Z" };
            for (int i = 0; i < destinationCounts.Length; i++)
            {
                if (destinationCounts[i] > 1)
                {
                    Debug.LogWarning($"[UWBAxisConversion] {destinationCounts[i]} raw axes map onto Unity {axisNames[i]}; their values are summed. Set one to \"none\" if that isn't intended.");
                }
            }
        }

        private static void Accumulate(string token, float value, ref Vector3 result)
        {
            if (TryParseToken(token, out int axisIndex, out float sign))
            {
                result[axisIndex] += sign * value;
            }
        }

        private static void CountDestination(string token, string fieldName, int[] destinationCounts)
        {
            if (TryParseToken(token, out int axisIndex, out _))
            {
                destinationCounts[axisIndex]++;
                return;
            }

            string axis = token?.Trim().ToLowerInvariant();
            bool intentionalDrop = string.IsNullOrEmpty(axis) || axis == "none" || axis == "0";
            if (!intentionalDrop)
            {
                Debug.LogWarning($"[UWBAxisConversion] {fieldName} has unknown token '{token}'; that axis is dropped.");
            }
        }

        private static bool TryParseToken(string token, out int axisIndex, out float sign)
        {
            axisIndex = -1;
            sign = 1f;

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string axis = token.Trim().ToLowerInvariant();
            if (axis == "none" || axis == "0")
            {
                return false;
            }

            if (axis[0] == '-')
            {
                sign = -1f;
                axis = axis.Substring(1);
            }
            else if (axis[0] == '+')
            {
                axis = axis.Substring(1);
            }

            switch (axis)
            {
                case "x":
                    axisIndex = 0;
                    return true;
                case "y":
                    axisIndex = 1;
                    return true;
                case "z":
                    axisIndex = 2;
                    return true;
                default:
                    return false;
            }
        }
    }

    [System.Serializable]
    public class UWBConfigData
    {
        [Header("UWB")]
        public string UWBSerialPort = "COM5";
        public int UWBBaudRate = 921600;

        [Tooltip("รับข้อมูลจาก Serial หรือ UDP datagram ที่บรรจุ NoopLoop binary frame")]
        public Fortal.UWB.UWBTransportMode transportMode = Fortal.UWB.UWBTransportMode.Serial;
        public string udpListenAddress = "0.0.0.0";
        public int udpListenPort = 9000;

        [Tooltip("Per-axis remap of the raw tracker position onto Unity space. Default sends raw X -> Unity X and raw Y -> Unity Z (raw Z dropped), matching the old YtoZ mode.")]
        public UWBAxisConversion axisConversion = new UWBAxisConversion();

        [Tooltip("Meters added to the device position after axis conversion, to shift the tracker origin into the play area. Example: with axisConversion rawYTo=\"-z\", set this Z to the field depth so a downward-increasing raw Y mirrors into 0..depth instead of going negative.")]
        public Vector3 UWBInputOffset = Vector3.zero;

        [Tooltip("Multiplier applied to raw UWB positions (real-world meters) to convert them into game-world units. 1 = 1 real meter maps to 1 Unity unit.")]
        public float metersToWorldScale = 1f;

        [Header("Arena Mapping (UWB X/Z -> Floor X/Z)")]
        [Tooltip("เปิดการ map พิกัดจริงเป็นพิกัด 2D ของฉาก Floor")]
        public bool useArenaMapping = true;
        public bool clampToArena = true;
        public Vector2 physicalMinMeters = Vector2.zero;
        public Vector2 physicalMaxMeters = new Vector2(6f, 4f);
        [Tooltip("ขอบเขตสนาม X/Z โดยเก็บ X ใน Vector2.x และ Z ใน Vector2.y")]
        public Vector2 arenaMin = new Vector2(-6f, -4f);
        public Vector2 arenaMax = new Vector2(6f, 4f);

        [Header("UWB Anchors")]
        [Tooltip("World-space meters position of each NoopLoop anchor. Matched by index to UWBManager's Scene Anchors / Anchor Device Ids.")]
        public Vector3[] UWBAnchorPositions = new Vector3[4];
    }
}
