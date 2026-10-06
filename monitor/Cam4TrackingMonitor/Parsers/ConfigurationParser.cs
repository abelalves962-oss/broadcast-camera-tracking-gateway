using System.Globalization;
using System.Text.RegularExpressions;
using Cam4TrackingMonitor.Models;

namespace Cam4TrackingMonitor.Parsers;

public sealed partial class ConfigurationParser
{
    [GeneratedRegex(@"(?<key>[^?\r\n:]+?)\s*:\s*(?<value>[^?\r\n]+)")]
    private static partial Regex LineRegex();

    [GeneratedRegex(@"X:\s*(?<x>\d+),\s*Y:\s*(?<y>\d+),\s*Z:\s*(?<z>\d+),\s*PAN:\s*(?<pan>\d+),\s*TILT:\s*(?<tilt>\d+),\s*ROLL:\s*(?<roll>\d+),\s*FOCO:\s*(?<focus>\d+),\s*ZOOM:\s*(?<zoom>\d+)")]
    private static partial Regex InversionRegex();

    [GeneratedRegex(@"OffsetX\s*:\s*(?<x>[-+0-9.,Ee]+),\s*OffsetY\s*:\s*(?<y>[-+0-9.,Ee]+),\s*OffsetZ\s*:\s*(?<z>[-+0-9.,Ee]+),\s*OffsetPan\s*:\s*(?<pan>[-+0-9.,Ee]+),\s*OffsetTilt\s*:\s*(?<tilt>[-+0-9.,Ee]+),\s*OffsetRoll\s*:\s*(?<roll>[-+0-9.,Ee]+)")]
    private static partial Regex OffsetRegex();

    public bool TryParse(string response, out TrackingBoxConfiguration configuration)
    {
        configuration = new TrackingBoxConfiguration();
        if (string.IsNullOrWhiteSpace(response)) return false;

        foreach (Match match in LineRegex().Matches(response))
        {
            var key = match.Groups["key"].Value.Trim();
            var value = match.Groups["value"].Value.Trim();
            if (key.Equals("Ver", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var version)) configuration.PresetVersion = version;
            else if (key.Equals("IP Server", StringComparison.OrdinalIgnoreCase)) configuration.Ip = value;
            else if (key.Equals("SN", StringComparison.OrdinalIgnoreCase)) configuration.SubnetMask = value;
            else if (key.Equals("GW", StringComparison.OrdinalIgnoreCase)) configuration.Gateway = value;
            else if (key.Equals("DNS", StringComparison.OrdinalIgnoreCase)) configuration.Dns = value;
            else if (key.Equals("Status DHCP", StringComparison.OrdinalIgnoreCase)) configuration.DhcpFlag = value.Equals("ON", StringComparison.OrdinalIgnoreCase);
            else if (key.Equals("Numero renders", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var count)) configuration.DestinationCount = count;
            else if (key.StartsWith("IP R", StringComparison.OrdinalIgnoreCase) && TryNumber(key, out var ipNumber)) configuration.Destinations[ipNumber - 1].Ip = value;
            else if (key.StartsWith("Porta R", StringComparison.OrdinalIgnoreCase) && TryNumber(key, out var portNumber) && int.TryParse(value, out var port)) configuration.Destinations[portNumber - 1].Port = port;
            else if (key.Contains("Ordem de envio dos eixos", StringComparison.OrdinalIgnoreCase)) configuration.AxisOrder = value.ToUpperInvariant();
            else if (key.Contains("Ordem de envio dos angulos", StringComparison.OrdinalIgnoreCase)) configuration.AngleOrder = value.ToUpperInvariant();
        }

        var inversion = InversionRegex().Match(response);
        if (inversion.Success)
        {
            bool IsSet(string name) => inversion.Groups[name].Value != "0";
            configuration.Inversions = new AxisInversions { X = IsSet("x"), Y = IsSet("y"), Z = IsSet("z"), Pan = IsSet("pan"), Tilt = IsSet("tilt"), Roll = IsSet("roll"), Focus = IsSet("focus"), Zoom = IsSet("zoom") };
        }

        var offsets = OffsetRegex().Match(response);
        if (offsets.Success)
        {
            double Read(string name) => double.TryParse(offsets.Groups[name].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
            configuration.Offsets = new TrackingOffsets { X = Read("x"), Y = Read("y"), Z = Read("z"), Pan = Read("pan"), Tilt = Read("tilt"), Roll = Read("roll") };
        }

        return response.Contains("Configuracao geral", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(configuration.Ip);
    }

    private static bool TryNumber(string key, out int number)
    {
        var character = key.LastOrDefault(char.IsDigit);
        number = character == default ? 0 : character - '0';
        return number is >= 1 and <= 7;
    }
}
