using System.Text.RegularExpressions;

namespace PencePerLitre.Shared;

public class PostcodeLookupEngine
{
    private byte[]? _packBytes;
    private double _minLong;
    private double _maxLong;
    private double _minLat;
    private double _maxLat;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;

    public void LoadPack(byte[] packBytes)
    {
        if (packBytes.Length < 3780)
        {
            throw new InvalidOperationException("Invalid postcode pack size.");
        }

        _packBytes = packBytes;

        // Check magic number "UKPP" (0x50504B55)
        var magic = BitConverter.ToUInt32(_packBytes, 0);
        if (magic != 1347439445)
        {
            throw new InvalidOperationException("Postcode data file does not have valid UKPP magic.");
        }

        // Bounding box extents (32 bytes at offset 16)
        _minLong = BitConverter.ToDouble(_packBytes, 16);
        _maxLong = BitConverter.ToDouble(_packBytes, 24);
        _minLat = BitConverter.ToDouble(_packBytes, 32);
        _maxLat = BitConverter.ToDouble(_packBytes, 40);

        _isInitialized = true;
    }

    public (bool Found, string CanonicalPostcode, double Lat, double Lon) Lookup(string postcode)
    {
        if (!_isInitialized || _packBytes == null)
        {
            return (false, postcode, 0, 0);
        }

        if (string.IsNullOrWhiteSpace(postcode))
        {
            return (false, postcode, 0, 0);
        }

        var canonical = FormatPostcode(postcode);
        if (string.IsNullOrEmpty(canonical) || canonical.Length < 2)
        {
            return (false, postcode, 0, 0);
        }

        int cCode;
        try
        {
            cCode = PackCode(canonical);
        }
        catch
        {
            return (false, postcode, 0, 0);
        }

        char c1 = canonical[0];
        char c2 = canonical[1];
        int c2_i = (c2 >= '0' && c2 <= '9') ? (c2 - '0') : (10 + c2 - 'A');
        int lutIndex = (c1 - 'A') * 36 + c2_i;

        if (lutIndex < 0 || lutIndex >= 26 * 36)
        {
            return (false, postcode, 0, 0);
        }

        int lpos = 16 + 32 + (lutIndex * 4);
        uint startPos = BitConverter.ToUInt32(_packBytes, lpos);
        uint endPos = BitConverter.ToUInt32(_packBytes, lpos + 4);

        int dataStart = 16 + 32 + (26 * 36 * 4) + 4;
        int pos = (int)startPos + dataStart;
        int end = (int)endPos + dataStart;

        int lastCode = 0;
        int lastLat = 0;
        int lastLong = 0;

        while (pos < end && pos < _packBytes.Length)
        {
            byte format = _packBytes[pos++];
            bool pcIsDelta = (format & 0x80) != 0;
            bool llIsDelta = (format & 0x40) != 0;

            int thisCode;
            if (pcIsDelta)
            {
                int delta = format & 0x3f;
                thisCode = lastCode + delta + 1;
            }
            else
            {
                if (pos + 3 > _packBytes.Length) break;
                byte ncA = _packBytes[pos++];
                byte ncB = _packBytes[pos++];
                byte ncC = _packBytes[pos++];
                thisCode = (ncC << 16) | (ncB << 8) | ncA;
            }

            int lat, lon;
            if (llIsDelta)
            {
                if (pos + 2 > _packBytes.Length) break;
                sbyte dLat = (sbyte)_packBytes[pos++];
                sbyte dLon = (sbyte)_packBytes[pos++];
                lat = lastLat + dLat;
                lon = lastLong + dLon;
            }
            else
            {
                if (pos + 4 > _packBytes.Length) break;
                ushort uLat = BitConverter.ToUInt16(_packBytes, pos);
                pos += 2;
                ushort uLon = BitConverter.ToUInt16(_packBytes, pos);
                pos += 2;
                lat = uLat;
                lon = uLon;
            }

            if (thisCode == cCode)
            {
                double realLat = _minLat + ((_maxLat - _minLat) * (lat / 65535.0));
                double realLon = _minLong + ((_maxLong - _minLong) * (lon / 65535.0));
                return (true, FormatDisplayPostcode(canonical), realLat, realLon);
            }

            lastCode = thisCode;
            lastLat = lat;
            lastLong = lon;
        }

        // If exact postcode was not found, try outward code (e.g. PO14 from PO14 3LG)
        if (canonical.Length == 7)
        {
            var outward = canonical[..4].Trim();
            if (outward.Length >= 2 && outward.Length <= 4)
            {
                var outwardResult = Lookup(outward);
                if (outwardResult.Found)
                {
                    return (true, postcode.ToUpperInvariant(), outwardResult.Lat, outwardResult.Lon);
                }
            }
        }

        return (false, postcode, 0, 0);
    }

    private static string FormatPostcode(string pc)
    {
        var clean = pc.Replace(" ", "").Trim().ToUpperInvariant();
        if (clean.Length < 2 || clean.Length > 7) return string.Empty;

        if (clean.Length <= 4)
        {
            return clean.PadRight(4);
        }

        var inward = clean[^3..];
        var outward = clean[..^3];
        return outward.PadRight(4) + inward;
    }

    private static string FormatDisplayPostcode(string canonical)
    {
        if (canonical.Length == 7)
        {
            var outward = canonical[..4].Trim();
            var inward = canonical[4..];
            return $"{outward} {inward}";
        }
        return canonical.Trim();
    }

    private static int PackCode(string postcode)
    {
        int EncodeAZ09Space(char x)
        {
            if (x == ' ') return 36;
            if (x >= 'A' && x <= 'Z') return x - 'A';
            if (x >= '0' && x <= '9') return x - '0' + 26;
            throw new ArgumentException($"Invalid character in postcode: {x}");
        }

        int Encode09(char x)
        {
            if (x >= '0' && x <= '9') return x - '0';
            throw new ArgumentException($"Invalid character in postcode: {x}");
        }

        int EncodeAZ(char x)
        {
            if (x >= 'A' && x <= 'Z') return x - 'A';
            throw new ArgumentException($"Invalid character in postcode: {x}");
        }

        if (postcode.Length == 4)
        {
            int c2 = 37 * EncodeAZ09Space(postcode[2]);
            int d2 = EncodeAZ09Space(postcode[3]);
            return c2 + d2;
        }

        if (postcode.Length == 7)
        {
            int c2 = 26 * 26 * 10 * 37 * EncodeAZ09Space(postcode[2]);
            int d2 = 26 * 26 * 10 * EncodeAZ09Space(postcode[3]);
            int e2 = 26 * 26 * Encode09(postcode[4]);
            int f2 = 26 * EncodeAZ(postcode[5]);
            int g2 = EncodeAZ(postcode[6]);
            return c2 + d2 + e2 + f2 + g2;
        }

        throw new ArgumentException($"Invalid postcode length: {postcode.Length}");
    }
}

