using System.Text;

namespace Argos.Simulation.Movement;

/// <summary>
/// Hash estável entre processos e máquinas (plan 16.5): FNV-1a de 64 bits sobre os bytes UTF-8 do texto. Nunca
/// <see cref="string.GetHashCode()"/>, que muda a cada processo e quebraria o determinismo (Art. IX.3).
/// </summary>
public static class StableHash
{
    private const ulong OffsetBasis = 14695981039346656037;
    private const ulong Prime = 1099511628211;

    public static ulong Fnv1a64(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var hash = OffsetBasis;
        foreach (var value in Encoding.UTF8.GetBytes(text))
        {
            hash ^= value;
            hash *= Prime;
        }

        return hash;
    }

    /// <summary>Fração em [0, 1) derivada do hash, com os 53 bits mais altos (a precisão de um <see cref="double"/>).</summary>
    public static double Fraction(string text) => (Fnv1a64(text) >> 11) / (double)(1UL << 53);
}
