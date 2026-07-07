using System.Runtime.InteropServices;

namespace ResetYourFuture.Application.Common;

/// <summary>Converts embedding vectors to/from the raw bytes stored in AssistantContentChunk.Embedding.</summary>
public static class EmbeddingCodec
{
    public static byte[] ToBytes(ReadOnlyMemory<float> vector) =>
        MemoryMarshal.AsBytes(vector.Span).ToArray();

    public static float[] ToFloatArray(byte[] bytes) =>
        MemoryMarshal.Cast<byte, float>(bytes).ToArray();
}
