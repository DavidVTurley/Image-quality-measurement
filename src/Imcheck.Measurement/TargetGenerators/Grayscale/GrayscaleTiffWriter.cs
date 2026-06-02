using System.Buffers.Binary;
using System.Text;
using OpenCvSharp;

namespace Imcheck.Measurement;

public static class GrayscaleTiffWriter
{
    private static readonly byte[] EciRgbV2GrayProfile = BuildEciRgbV2GrayProfile();

    public static bool Write(string outputPath, Mat image)
    {
        if (image.Empty())
        {
            throw new ArgumentException("Image is empty.", nameof(image));
        }

        var extension = Path.GetExtension(outputPath).ToLowerInvariant();
        if (extension is not ".tif" and not ".tiff")
        {
            return Cv2.ImWrite(outputPath, image);
        }

        File.WriteAllBytes(outputPath, BuildTiff(image));
        return true;
    }

    private static byte[] BuildTiff(Mat image)
    {
        var width = image.Width;
        var height = image.Height;
        var pixelData = BuildGrayPixels(image);
        var profile = EciRgbV2GrayProfile;

        const int entryCount = 13;
        const int headerLength = 8;
        const int ifdLength = 2 + entryCount * 12 + 4;
        var valueStart = headerLength + ifdLength;
        var xResolutionOffset = valueStart;
        var yResolutionOffset = valueStart + 8;
        var profileOffset = valueStart + 16;
        var imageOffset = profileOffset + profile.Length + PaddingFor(profile.Length);
        var totalLength = imageOffset + pixelData.Length;

        var buffer = new byte[totalLength];
        var cursor = 0;
        WriteByte(buffer, ref cursor, 0x4D);
        WriteByte(buffer, ref cursor, 0x4D);
        WriteUInt16(buffer, ref cursor, 42);
        WriteUInt32(buffer, ref cursor, headerLength);
        WriteUInt16(buffer, ref cursor, entryCount);

        WriteEntry(buffer, ref cursor, 256, 4, 1, width);
        WriteEntry(buffer, ref cursor, 257, 4, 1, height);
        WriteEntry(buffer, ref cursor, 258, 3, 1, 8);
        WriteEntry(buffer, ref cursor, 259, 3, 1, 1);
        WriteEntry(buffer, ref cursor, 262, 3, 1, 1);
        WriteEntry(buffer, ref cursor, 273, 4, 1, imageOffset);
        WriteEntry(buffer, ref cursor, 277, 3, 1, 1);
        WriteEntry(buffer, ref cursor, 278, 4, 1, height);
        WriteEntry(buffer, ref cursor, 279, 4, 1, pixelData.Length);
        WriteEntry(buffer, ref cursor, 282, 5, 1, xResolutionOffset);
        WriteEntry(buffer, ref cursor, 283, 5, 1, yResolutionOffset);
        WriteEntry(buffer, ref cursor, 296, 3, 1, 2);
        WriteEntry(buffer, ref cursor, 34675, 7, profile.Length, profileOffset);
        WriteUInt32(buffer, ref cursor, 0);

        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(xResolutionOffset), 300);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(xResolutionOffset + 4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(yResolutionOffset), 300);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(yResolutionOffset + 4), 1);
        profile.CopyTo(buffer.AsSpan(profileOffset));
        pixelData.CopyTo(buffer.AsSpan(imageOffset));

        return buffer;
    }

    private static byte[] BuildGrayPixels(Mat image)
    {
        var pixels = new byte[image.Width * image.Height];
        var index = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                pixels[index++] = image.At<Vec3b>(y, x).Item0;
            }
        }

        return pixels;
    }

    private static byte[] BuildEciRgbV2GrayProfile()
    {
        const int tableLength = 256;
        var toneCurve = new ushort[tableLength];
        var threshold = 9.033 * 0.008856;
        for (var i = 0; i < tableLength; i++)
        {
            var encoded = i / (double)(tableLength - 1);
            var linear = encoded < threshold
                ? encoded / 9.033
                : Math.Pow((encoded + 0.16) / 1.16, 3);
            toneCurve[i] = (ushort)Math.Clamp((int)Math.Round(Math.Clamp(linear, 0, 1) * 65535), 0, 65535);
        }

        var descTag = BuildDescriptionTag("eciRGB v2 Gray (L* TRC, D50)");
        var copyrightTag = BuildTextTag("ECI European Color Initiative. Built from published specification.");
        var whitePointTag = BuildXyzTag(0.96420, 1.00000, 0.82491);
        var toneCurveTag = BuildCurveTag(toneCurve);

        var tags = new[]
        {
            new IccTag("desc", descTag),
            new IccTag("cprt", copyrightTag),
            new IccTag("wtpt", whitePointTag),
            new IccTag("kTRC", toneCurveTag)
        };

        const int headerLength = 128;
        var tagDirectoryLength = 4 + tags.Length * 12;
        var dataOffset = headerLength + tagDirectoryLength;
        var offsets = new int[tags.Length];
        for (var i = 0; i < tags.Length; i++)
        {
            offsets[i] = dataOffset;
            dataOffset += tags[i].Data.Length;
        }

        var profile = new byte[dataOffset];
        WriteUInt32(profile, 0, dataOffset);
        WriteAscii(profile, 4, "    ");
        WriteUInt32(profile, 8, 0x02100000);
        WriteAscii(profile, 12, "mntr");
        WriteAscii(profile, 16, "GRAY");
        WriteAscii(profile, 20, "XYZ ");
        WriteAscii(profile, 36, "acsp");
        WriteS15Fixed16(profile, 68, 0.96420);
        WriteS15Fixed16(profile, 72, 1.00000);
        WriteS15Fixed16(profile, 76, 0.82491);

        WriteUInt32(profile, headerLength, tags.Length);
        for (var i = 0; i < tags.Length; i++)
        {
            var offset = headerLength + 4 + i * 12;
            WriteAscii(profile, offset, tags[i].Signature);
            WriteUInt32(profile, offset + 4, offsets[i]);
            WriteUInt32(profile, offset + 8, tags[i].Data.Length);
            tags[i].Data.CopyTo(profile.AsSpan(offsets[i]));
        }

        return profile;
    }

    private static byte[] BuildDescriptionTag(string value)
    {
        var text = Encoding.ASCII.GetBytes(value);
        var length = 4 + 4 + 4 + text.Length + 1;
        var tag = new byte[length + PaddingFor(length)];
        WriteAscii(tag, 0, "desc");
        WriteUInt32(tag, 8, text.Length + 1);
        text.CopyTo(tag.AsSpan(12));
        return tag;
    }

    private static byte[] BuildTextTag(string value)
    {
        var text = Encoding.ASCII.GetBytes(value);
        var length = 4 + 4 + text.Length + 1;
        var tag = new byte[length + PaddingFor(length)];
        WriteAscii(tag, 0, "text");
        text.CopyTo(tag.AsSpan(8));
        return tag;
    }

    private static byte[] BuildXyzTag(double x, double y, double z)
    {
        var tag = new byte[20];
        WriteAscii(tag, 0, "XYZ ");
        WriteS15Fixed16(tag, 8, x);
        WriteS15Fixed16(tag, 12, y);
        WriteS15Fixed16(tag, 16, z);
        return tag;
    }

    private static byte[] BuildCurveTag(IReadOnlyList<ushort> values)
    {
        var tag = new byte[4 + 4 + 4 + values.Count * 2];
        WriteAscii(tag, 0, "curv");
        WriteUInt32(tag, 8, values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(tag.AsSpan(12 + i * 2), values[i]);
        }

        return tag;
    }

    private static int PaddingFor(int length)
    {
        return (4 - length % 4) % 4;
    }

    private static void WriteEntry(byte[] buffer, ref int cursor, ushort tag, ushort type, int count, int value)
    {
        WriteUInt16(buffer, ref cursor, tag);
        WriteUInt16(buffer, ref cursor, type);
        WriteUInt32(buffer, ref cursor, count);
        if (type == 3 && count == 1)
        {
            WriteUInt16(buffer, ref cursor, value);
            WriteUInt16(buffer, ref cursor, 0);
            return;
        }

        WriteUInt32(buffer, ref cursor, value);
    }

    private static void WriteByte(byte[] buffer, ref int cursor, byte value)
    {
        buffer[cursor++] = value;
    }

    private static void WriteUInt16(byte[] buffer, ref int cursor, int value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(cursor), (ushort)value);
        cursor += 2;
    }

    private static void WriteUInt32(byte[] buffer, ref int cursor, int value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(cursor), (uint)value);
        cursor += 4;
    }

    private static void WriteUInt32(byte[] buffer, int offset, int value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset), (uint)value);
    }

    private static void WriteAscii(byte[] buffer, int offset, string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            buffer[offset + i] = (byte)value[i];
        }
    }

    private static void WriteS15Fixed16(byte[] buffer, int offset, double value)
    {
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(offset), (int)Math.Round(value * 65536));
    }

    private sealed record IccTag(string Signature, byte[] Data);
}
