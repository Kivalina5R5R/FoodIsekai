using UnityEngine;

namespace PaperArena
{
    /// <summary>
    /// Free-form remap of the tracker's raw X/Y/Z onto Unity's X/Y/Z. Each field names where one
    /// raw axis is sent, using a signed token: "x", "-x", "y", "-y", "z", "-z", or "none"/"0" to
    /// drop that axis. Lets a mis-calibrated NoopLoop frame (e.g. long side reported as Y) be
    /// corrected from config without code changes.
    /// </summary>
    [System.Serializable]
    public class UWBAxisConversion
    {
        [Tooltip("Where the tracker's raw X goes in Unity. Tokens: x, -x, y, -y, z, -z, none.")]
        public string rawXTo = "x";
        [Tooltip("Where the tracker's raw Y goes in Unity. Tokens: x, -x, y, -y, z, -z, none.")]
        public string rawYTo = "z";
        [Tooltip("Where the tracker's raw Z goes in Unity. Tokens: x, -x, y, -y, z, -z, none.")]
        public string rawZTo = "none";

        /// <summary>Maps a raw tracker position onto Unity space per the configured tokens.</summary>
        public Vector3 Apply(Vector3 raw)
        {
            Vector3 result = Vector3.zero;
            Accumulate(rawXTo, raw.x, ref result);
            Accumulate(rawYTo, raw.y, ref result);
            Accumulate(rawZTo, raw.z, ref result);
            return result;
        }

        /// <summary>
        /// Logs warnings for unknown tokens or for multiple raw axes mapping onto the same Unity
        /// axis (their values are summed, which is rarely intended). Never changes behavior; call
        /// once at setup rather than per frame.
        /// </summary>
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

        /// <summary>Parses a token into a Unity axis index (0=X, 1=Y, 2=Z) and sign. Returns false for "none"/blank/unknown.</summary>
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

        [Tooltip("Per-axis remap of the raw tracker position onto Unity space. Default sends raw X -> Unity X and raw Y -> Unity Z (raw Z dropped), matching the old YtoZ mode.")]
        public UWBAxisConversion axisConversion = new UWBAxisConversion();

        [Tooltip("Meters added to the device position after axis conversion, to shift the tracker origin into the play area. Example: with axisConversion rawYTo=\"-z\", set this Z to the field depth so a downward-increasing raw Y mirrors into 0..depth instead of going negative.")]
        public Vector3 UWBInputOffset = Vector3.zero;

        [Tooltip("Multiplier applied to raw UWB positions (real-world meters) to convert them into game-world units. 1 = 1 real meter maps to 1 Unity unit.")]
        public float metersToWorldScale = 1f;

        [Header("UWB Anchors")]
        [Tooltip("World-space meters position of each NoopLoop anchor. Matched by index to UWBManager's Scene Anchors / Anchor Device Ids.")]
        public Vector3[] UWBAnchorPositions = new Vector3[4];
    }
}