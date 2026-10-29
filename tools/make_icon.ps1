param(
    [string]$SourceImage = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$ProjectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$AssetsDir = Join-Path $ProjectRoot "assets"
if (-not (Test-Path $AssetsDir)) {
    New-Item -ItemType Directory -Force -Path $AssetsDir | Out-Null
}

$RawCopy = Join-Path $AssetsDir "logo_raw.png"
if (-not [string]::IsNullOrWhiteSpace($SourceImage) -and (Test-Path $SourceImage)) {
    Copy-Item $SourceImage $RawCopy -Force
}
if (-not (Test-Path $RawCopy)) {
    throw "Source image not found at $RawCopy. Pass -SourceImage <path> to import a new source logo."
}

$LogoPng = Join-Path $AssetsDir "logo.png"
$AppIco = Join-Path $AssetsDir "app.ico"

# Use inline C# for fast corner flood-fill background removal and multi-resolution ICO writing
Add-Type -ReferencedAssemblies "System.Drawing" -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class IconBuilder
{
    public static void BuildLogoAndIco(string srcPath, string outPngPath, string outIcoPath)
    {
        using (Bitmap orig = new Bitmap(srcPath))
        using (Bitmap work = new Bitmap(256, 256, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(work))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(orig, new Rectangle(0, 0, 256, 256));
            }

            // Flood-fill outer near-white background from the 4 corners so icon has clean transparent edges
            bool[,] visited = new bool[256, 256];
            Queue<Point> q = new Queue<Point>();
            Point[] seeds = new Point[] { new Point(0, 0), new Point(255, 0), new Point(0, 255), new Point(255, 255) };
            foreach (Point s in seeds)
            {
                visited[s.X, s.Y] = true;
                q.Enqueue(s);
            }

            int[] dx = new int[] { 1, -1, 0, 0 };
            int[] dy = new int[] { 0, 0, 1, -1 };

            while (q.Count > 0)
            {
                Point p = q.Dequeue();
                Color c = work.GetPixel(p.X, p.Y);
                if (c.A > 0 && c.R >= 240 && c.G >= 240 && c.B >= 240)
                {
                    work.SetPixel(p.X, p.Y, Color.FromArgb(0, 255, 255, 255));
                    for (int dir = 0; dir < 4; dir++)
                    {
                        int nx = p.X + dx[dir];
                        int ny = p.Y + dy[dir];
                        if (nx >= 0 && nx < 256 && ny >= 0 && ny < 256 && !visited[nx, ny])
                        {
                            visited[nx, ny] = true;
                            q.Enqueue(new Point(nx, ny));
                        }
                    }
                }
            }

            work.Save(outPngPath, ImageFormat.Png);

            int[] sizes = new int[] { 256, 64, 48, 32, 16 };
            List<byte[]> pngFrames = new List<byte[]>();
            foreach (int sz in sizes)
            {
                using (Bitmap resized = new Bitmap(sz, sz, PixelFormat.Format32bppArgb))
                {
                    using (Graphics gr = Graphics.FromImage(resized))
                    {
                        gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        gr.SmoothingMode = SmoothingMode.HighQuality;
                        gr.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        gr.Clear(Color.Transparent);
                        gr.DrawImage(work, new Rectangle(0, 0, sz, sz));
                    }
                    using (MemoryStream ms = new MemoryStream())
                    {
                        resized.Save(ms, ImageFormat.Png);
                        pngFrames.Add(ms.ToArray());
                    }
                }
            }

            using (FileStream fs = File.Create(outIcoPath))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write((ushort)0); // Reserved
                bw.Write((ushort)1); // Type = 1 (ICO)
                bw.Write((ushort)sizes.Length);

                uint offset = (uint)(6 + 16 * sizes.Length);
                for (int i = 0; i < sizes.Length; i++)
                {
                    int sz = sizes[i];
                    bw.Write((byte)(sz >= 256 ? 0 : sz));
                    bw.Write((byte)(sz >= 256 ? 0 : sz));
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((ushort)1);
                    bw.Write((ushort)32);
                    bw.Write((uint)pngFrames[i].Length);
                    bw.Write(offset);
                    offset += (uint)pngFrames[i].Length;
                }

                for (int i = 0; i < pngFrames.Count; i++)
                {
                    bw.Write(pngFrames[i]);
                }
            }
        }
    }
}
"@

[IconBuilder]::BuildLogoAndIco($RawCopy, $LogoPng, $AppIco)
Write-Host "Successfully built transparent logo ($LogoPng) and multi-size icon ($AppIco)."
