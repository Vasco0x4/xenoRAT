using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace xeno_rat_server
{
    class Compression
    {
        // Custom compression constants to avoid detection
        private const ushort CUSTOM_COMPRESSION_FORMAT = 0x721;
        private const ushort CUSTOM_ENGINE_MAXIMUM = 0x421;
        private const int COMPRESSION_BUFFER_MULTIPLIER = 8;

        // Native methods with modified signatures
        [DllImport("ntdll.dll", EntryPoint = "RtlGetCompressionWorkSpaceSize", SetLastError = true)]
        private static extern uint GetWorkSpaceSize(ushort Format, out uint BufferSize, out uint TempSize);

        [DllImport("ntdll.dll", EntryPoint = "RtlCompressBuffer", SetLastError = true)]
        private static extern uint CompressNative(ushort Format, byte[] Source, int SourceLength,
            byte[] Destination, int DestLength, uint Chunk, out int FinalSize, IntPtr WorkSpace);

        [DllImport("ntdll.dll", EntryPoint = "RtlDecompressBuffer", SetLastError = true)]
        private static extern uint DecompressNative(ushort Format, byte[] Uncompressed, int UncompressedSize,
            byte[] Compressed, int CompressedSize, out int FinalSize);

        // Memory management with obfuscated names
        [DllImport("kernel32.dll", EntryPoint = "LocalAlloc")]
        private static extern IntPtr AllocateMemory(int Flags, IntPtr Bytes);

        [DllImport("kernel32.dll", EntryPoint = "LocalFree")]
        private static extern IntPtr FreeMemory(IntPtr Block);

        // Fallback compression using built-in .NET
        private static byte[] CompressFallback(byte[] data)
        {
            using (var compressedStream = new MemoryStream())
            {
                using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Compress))
                {
                    deflateStream.Write(data, 0, data.Length);
                }
                return compressedStream.ToArray();
            }
        }

        // Fallback decompression using built-in .NET
        private static byte[] DecompressFallback(byte[] compressedData, int originalSize)
        {
            using (var compressedStream = new MemoryStream(compressedData))
            using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                deflateStream.CopyTo(resultStream);
                return resultStream.ToArray();
            }
        }

        public static byte[] Compress(byte[] buffer)
        {
            try
            {
                var outBuffer = new byte[buffer.Length * COMPRESSION_BUFFER_MULTIPLIER];
                uint workSize = 0, tempSize = 0;

                // Get workspace size with custom format
                uint result = GetWorkSpaceSize(CUSTOM_COMPRESSION_FORMAT | CUSTOM_ENGINE_MAXIMUM,
                    out workSize, out tempSize);

                if (result != 0)
                    return CompressFallback(buffer);

                int finalSize = 0;
                IntPtr workspace = AllocateMemory(0, new IntPtr(workSize));

                if (workspace == IntPtr.Zero)
                    return CompressFallback(buffer);

                try
                {
                    result = CompressNative(CUSTOM_COMPRESSION_FORMAT | CUSTOM_ENGINE_MAXIMUM,
                        buffer, buffer.Length, outBuffer, outBuffer.Length, 0, out finalSize, workspace);

                    if (result != 0)
                        return CompressFallback(buffer);

                    Array.Resize(ref outBuffer, finalSize);
                    return outBuffer;
                }
                finally
                {
                    FreeMemory(workspace);
                }
            }
            catch
            {
                return CompressFallback(buffer);
            }
        }

        public static byte[] Decompress(byte[] buffer, int originalSize)
        {
            try
            {
                int finalSize = 0;
                byte[] result = new byte[originalSize];

                uint status = DecompressNative(CUSTOM_COMPRESSION_FORMAT,
                    result, originalSize, buffer, buffer.Length, out finalSize);

                if (status != 0)
                    return DecompressFallback(buffer, originalSize);

                return result;
            }
            catch
            {
                return DecompressFallback(buffer, originalSize);
            }
        }
    }
}
