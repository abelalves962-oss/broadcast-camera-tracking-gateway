namespace Cam4TrackingMonitor.Models;

public sealed class FreeDDestination
{
    public int Number { get; set; }
    public string Ip { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 1111;

    public FreeDDestination Clone() => new() { Number = Number, Ip = Ip, Port = Port };
}

public sealed class AxisInversions
{
    public bool X { get; set; }
    public bool Y { get; set; }
    public bool Z { get; set; }
    public bool Pan { get; set; }
    public bool Tilt { get; set; }
    public bool Roll { get; set; }
    public bool Focus { get; set; }
    public bool Zoom { get; set; }

    public AxisInversions Clone() => (AxisInversions)MemberwiseClone();
}

public sealed class TrackingOffsets
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double Pan { get; set; }
    public double Tilt { get; set; }
    public double Roll { get; set; }

    public TrackingOffsets Clone() => (TrackingOffsets)MemberwiseClone();
}

public sealed class TrackingBoxConfiguration
{
    public int? PresetVersion { get; set; }
    public string Ip { get; set; } = "192.0.2.10";
    public string SubnetMask { get; set; } = "255.255.255.0";
    public string Gateway { get; set; } = "192.0.2.1";
    public string Dns { get; set; } = "0.0.0.0";
    public bool DhcpFlag { get; set; }
    public int DestinationCount { get; set; }
    public List<FreeDDestination> Destinations { get; set; } = Enumerable.Range(1, 7)
        .Select(number => new FreeDDestination { Number = number }).ToList();
    public string AxisOrder { get; set; } = "YXZ";
    public string AngleOrder { get; set; } = "PTR";
    public AxisInversions Inversions { get; set; } = new();
    public TrackingOffsets Offsets { get; set; } = new();

    public TrackingBoxConfiguration Clone() => new()
    {
        PresetVersion = PresetVersion,
        Ip = Ip,
        SubnetMask = SubnetMask,
        Gateway = Gateway,
        Dns = Dns,
        DhcpFlag = DhcpFlag,
        DestinationCount = DestinationCount,
        Destinations = Destinations.Select(destination => destination.Clone()).ToList(),
        AxisOrder = AxisOrder,
        AngleOrder = AngleOrder,
        Inversions = Inversions.Clone(),
        Offsets = Offsets.Clone()
    };
}
