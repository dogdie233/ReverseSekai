using System.Numerics;

namespace SekaiApiModel.Shared;

public struct Vector3 : IEquatable<Vector3>, IEqualityOperators<Vector3, Vector3, bool>, IFormattable
{
    public float x;
    public float y;
    public float z;

    public bool Equals(Vector3 other)
    {
        return x.Equals(other.x) && y.Equals(other.y) && z.Equals(other.z);
    }

    public override bool Equals(object? obj)
    {
        return obj is Vector3 other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(x, y, z);
    }

    public static bool operator ==(Vector3 left, Vector3 right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Vector3 left, Vector3 right)
    {
        return !left.Equals(right);
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        FormattableString formattable = $"{nameof(x)}: {x}, {nameof(y)}: {y}, {nameof(z)}: {z}";
        return formattable.ToString(formatProvider);
    }

    public override string ToString()
    {
        return $"{nameof(x)}: {x}, {nameof(y)}: {y}, {nameof(z)}: {z}";
    }
}