using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace StickyTickets.Data
{
    /// <summary>
    /// Validates and encodes pasted images using Windows built-in WIC (PNG/JPEG).
    /// Prevents decompression bombs by enforcing strict byte size and pixel dimension limits.
    /// </summary>
    public static class WicImageSafety
    {
        public const int MaxImageBytes = 4 * 1024 * 1024; // 4 MB limit per image
        public const int MaxPixelDimension = 2560;

        public static byte[] EncodeSafePng(BitmapSource source, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (source == null) throw new ArgumentNullException("source");

            if (source.PixelWidth <= 0 || source.PixelHeight <= 0 ||
                source.PixelWidth > MaxPixelDimension || source.PixelHeight > MaxPixelDimension)
            {
                throw new InvalidOperationException("图片尺寸超出安全上限 (最大支持 2560x2560 像素)。");
            }

            width = source.PixelWidth;
            height = source.PixelHeight;

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (MemoryStream ms = new MemoryStream())
            {
                encoder.Save(ms);
                if (ms.Length > MaxImageBytes)
                {
                    throw new InvalidOperationException("编码后图片体积超过 4MB 安全上限。");
                }
                return ms.ToArray();
            }
        }

        public static BitmapImage DecodeSafeImage(byte[] blob)
        {
            if (blob == null || blob.Length == 0 || blob.Length > MaxImageBytes)
                return null;

            using (MemoryStream ms = new MemoryStream(blob, false))
            {
                BitmapDecoder decoder = BitmapDecoder.Create(
                    ms,
                    BitmapCreateOptions.DelayCreation,
                    BitmapCacheOption.None);

                if (decoder.Frames.Count == 0) return null;
                BitmapFrame frame = decoder.Frames[0];
                if (frame.PixelWidth > MaxPixelDimension || frame.PixelHeight > MaxPixelDimension)
                {
                    throw new InvalidOperationException("拒绝加载超高分辨率异常图片。");
                }
            }

            using (MemoryStream ms2 = new MemoryStream(blob, false))
            {
                BitmapImage img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = ms2;
                img.EndInit();
                img.Freeze();
                return img;
            }
        }
    }
}
