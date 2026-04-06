namespace StructUnion.IntegrationTests.ComplexTypes;

public struct Point
{
    public double X { get; set; }
    public double Y { get; set; }
}

public enum Color : byte { Red, Green, Blue }

public enum Status { Active = 1, Inactive = 2, Pending = 3 }
